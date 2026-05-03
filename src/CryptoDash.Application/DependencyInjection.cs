using CryptoDash.Application.Services;
using Microsoft.Extensions.DependencyInjection;

namespace CryptoDash.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        services.AddScoped<AuthService>();
        services.AddScoped<PortfolioService>();
        services.AddScoped<MarketService>();
        services.AddScoped<AportsService>();
        services.AddScoped<NewsService>();
        return services;
    }
}
