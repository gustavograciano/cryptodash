using CryptoDash.Application.Abstractions;
using CryptoDash.Infrastructure.Auth;
using CryptoDash.Infrastructure.External.AlternativeMe;
using CryptoDash.Infrastructure.External.Anthropic;
using CryptoDash.Infrastructure.External.CoinGecko;
using CryptoDash.Infrastructure.External.CryptoPanic;
using CryptoDash.Infrastructure.Jobs;
using CryptoDash.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Quartz;
using Refit;

namespace CryptoDash.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration config)
    {
        var conn = config.GetConnectionString("Default")
                   ?? throw new InvalidOperationException("ConnectionStrings:Default ausente");

        var provider = config["Database:Provider"] ?? "Npgsql";
        services.AddDbContext<AppDbContext>(opts =>
        {
            if (provider.Equals("Sqlite", StringComparison.OrdinalIgnoreCase))
                opts.UseSqlite(conn);
            else
                opts.UseNpgsql(conn).UseSnakeCaseNamingConvention();
        });
        services.AddScoped<IAppDbContext>(sp => sp.GetRequiredService<AppDbContext>());

        services.AddMemoryCache();
        services.AddScoped<IPasswordHasher, BCryptPasswordHasher>();

        services.Configure<JwtOptions>(config.GetSection("Jwt"));
        services.AddScoped<IJwtTokenService, JwtTokenService>();

        var coingeckoBase = config["CoinGecko:BaseUrl"] ?? "https://api.coingecko.com/api/v3";
        services.AddRefitClient<ICoinGeckoApi>()
            .ConfigureHttpClient(c =>
            {
                c.BaseAddress = new Uri(coingeckoBase);
                c.Timeout = TimeSpan.FromSeconds(15);
                c.DefaultRequestHeaders.Add("User-Agent", "CryptoDash/1.0");
                c.DefaultRequestHeaders.Add("Accept", "application/json");
            });
        services.AddScoped<IPriceFeed, CoinGeckoPriceFeed>();
        services.AddScoped<IMarketDataFeed, CoinGeckoMarketDataFeed>();

        var altBase = config["AlternativeMe:BaseUrl"] ?? "https://api.alternative.me";
        services.AddRefitClient<IAlternativeMeApi>()
            .ConfigureHttpClient(c =>
            {
                c.BaseAddress = new Uri(altBase);
                c.Timeout = TimeSpan.FromSeconds(15);
            });
        services.AddScoped<IFearGreedFeed, AlternativeMeFearGreedFeed>();

        var cpBase = config["CryptoPanic:BaseUrl"] ?? "https://cryptopanic.com/api/v1";
        services.AddRefitClient<ICryptoPanicApi>()
            .ConfigureHttpClient(c => { c.BaseAddress = new Uri(cpBase); c.Timeout = TimeSpan.FromSeconds(15); });
        services.AddScoped<INewsFeed, CryptoPanicNewsFeed>();

        var anthBase = config["Anthropic:BaseUrl"] ?? "https://api.anthropic.com";
        services.AddRefitClient<IAnthropicApi>()
            .ConfigureHttpClient(c => { c.BaseAddress = new Uri(anthBase); c.Timeout = TimeSpan.FromSeconds(60); });
        services.AddScoped<ILlmDigestGenerator, AnthropicDigestGenerator>();

        services.AddQuartz(q =>
        {
            var snapKey = new JobKey("PriceSnapshotJob");
            q.AddJob<PriceSnapshotJob>(opts => opts.WithIdentity(snapKey));
            q.AddTrigger(t => t.ForJob(snapKey)
                .WithIdentity("PriceSnapshotJob-trigger")
                .StartNow()
                .WithSimpleSchedule(s => s.WithIntervalInMinutes(15).RepeatForever()));

            var histKey = new JobKey("PriceHistoryJob");
            q.AddJob<PriceHistoryJob>(opts => opts.WithIdentity(histKey));
            q.AddTrigger(t => t.ForJob(histKey)
                .WithIdentity("PriceHistoryJob-trigger")
                .StartAt(DateTimeOffset.UtcNow.AddMinutes(2))
                .WithCronSchedule("0 0 1 * * ?"));

            var mktKey = new JobKey("MarketIndicatorsJob");
            q.AddJob<MarketIndicatorsJob>(opts => opts.WithIdentity(mktKey));
            q.AddTrigger(t => t.ForJob(mktKey)
                .WithIdentity("MarketIndicatorsJob-trigger")
                .StartAt(DateTimeOffset.UtcNow.AddMinutes(3))
                .WithCronSchedule("0 0 2 * * ?"));

            var newsKey = new JobKey("NewsFetchJob");
            q.AddJob<NewsFetchJob>(opts => opts.WithIdentity(newsKey));
            q.AddTrigger(t => t.ForJob(newsKey)
                .WithIdentity("NewsFetchJob-trigger")
                .WithCronSchedule("0 0 */2 * * ?"));

            var aiKey = new JobKey("AIDigestJob");
            q.AddJob<AIDigestJob>(opts => opts.WithIdentity(aiKey));
            q.AddTrigger(t => t.ForJob(aiKey)
                .WithIdentity("AIDigestJob-trigger")
                .WithCronSchedule("0 0 7 * * ?"));

            var remKey = new JobKey("AportReminderJob");
            q.AddJob<AportReminderJob>(opts => opts.WithIdentity(remKey));
            q.AddTrigger(t => t.ForJob(remKey)
                .WithIdentity("AportReminderJob-trigger")
                .WithCronSchedule("0 0 9 * * ?"));
        });
        services.AddQuartzHostedService(o => o.WaitForJobsToComplete = true);

        return services;
    }
}
