# -------------------- Build stage --------------------
FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /src

# Copy solution and restore
COPY . .
WORKDIR /src/Server
RUN dotnet restore

# Publish
RUN dotnet publish -c Release -o /app --no-restore

# -------------------- Runtime stage --------------------
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS runtime
WORKDIR /app

# App files
COPY --from=build /app ./

# Create data dir for SQLite (SqliteUserRepo uses ContentRoot/App_Data)
RUN mkdir -p /app/App_Data

# Non-root user (built-in)
USER $APP_UID

# ASP.NET binds on 8080 inside the container
ENV ASPNETCORE_URLS=http://0.0.0.0:8080 \
    DOTNET_EnableDiagnostics=0

EXPOSE 8080

# Start
ENTRYPOINT ["dotnet", "Bomberman.Server.dll"]
