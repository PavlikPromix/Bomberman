# ---- Build stage (SDK 9.0) ----
FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /src
COPY . .
WORKDIR /src/Server
RUN dotnet restore
RUN dotnet publish -c Release -o /app --no-restore

# ---- Runtime stage (ASP.NET 9.0) ----
FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS runtime
WORKDIR /app
COPY --from=build /app ./

# Ensure the SQLite data directory exists and is writable by the non-root user
# Official images provide $APP_UID for the built-in 'app' user.
RUN mkdir -p /app/App_Data && chown -R $APP_UID /app

# Run as the built-in non-root user
USER $APP_UID

ENV ASPNETCORE_URLS=http://0.0.0.0:5202 \
    ASPNETCORE_ENVIRONMENT=Production \
    Logging__LogLevel__Default=Information \
    DOTNET_EnableDiagnostics=0
EXPOSE 5202

ENTRYPOINT ["dotnet", "Bomberman.Server.dll"]
