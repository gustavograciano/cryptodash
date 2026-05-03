FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src
COPY *.sln ./
COPY src/CryptoDash.Domain/*.csproj src/CryptoDash.Domain/
COPY src/CryptoDash.Application/*.csproj src/CryptoDash.Application/
COPY src/CryptoDash.Infrastructure/*.csproj src/CryptoDash.Infrastructure/
COPY src/CryptoDash.Api/*.csproj src/CryptoDash.Api/
RUN dotnet restore CryptoDash.sln

COPY . .
RUN dotnet publish src/CryptoDash.Api/CryptoDash.Api.csproj -c Release -o /app/publish /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS runtime
WORKDIR /app
COPY --from=build /app/publish .
EXPOSE 8080
ENTRYPOINT ["dotnet", "CryptoDash.Api.dll"]
