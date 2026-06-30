# ── Build stage ──────────────────────────────────────────────────────────────
FROM mcr.microsoft.com/dotnet/sdk:6.0 AS build
WORKDIR /src

COPY Middleware_Demo.csproj .
RUN dotnet restore

COPY . .
RUN dotnet publish Middleware_Demo.csproj -c Release -o /publish

# ── Runtime stage ─────────────────────────────────────────────────────────────
FROM mcr.microsoft.com/dotnet/aspnet:6.0

# Install Apache2 with proxy modules
RUN apt-get update \
    && apt-get install -y --no-install-recommends apache2 curl \
    && a2enmod proxy proxy_http headers \
    && apt-get clean \
    && rm -rf /var/lib/apt/lists/*

WORKDIR /app

COPY --from=build /publish ./

# Apache virtual host (replaces the default site)
COPY Docker/apache/xrculture.conf /etc/apache2/sites-available/000-default.conf

# Entrypoint script
COPY Docker/entrypoint.sh /entrypoint.sh
RUN chmod +x /entrypoint.sh

VOLUME /app/Resources

ENV ASPNETCORE_URLS=http://127.0.0.1:5000
ENV ASPNETCORE_ENVIRONMENT=Production
ENV ASPNETCORE_FORWARDEDHEADERS_ENABLED=true

EXPOSE 80

HEALTHCHECK --interval=30s --timeout=10s --start-period=15s --retries=3 \
    CMD curl -f http://localhost/ || exit 1

ENTRYPOINT ["/entrypoint.sh"]
