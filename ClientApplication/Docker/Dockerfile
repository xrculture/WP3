# ── Build stage ──────────────────────────────────────────────────────────────
FROM mcr.microsoft.com/dotnet/sdk:6.0 AS build
WORKDIR /src

COPY Middleware_Demo.csproj .
RUN dotnet restore -r linux-x64

COPY . .
RUN dotnet publish Middleware_Demo.csproj \
        -c Release \
        -r linux-x64 \
        --self-contained true \
        --no-restore \
        -o /publish

# ── Runtime stage ─────────────────────────────────────────────────────────────
FROM mcr.microsoft.com/dotnet/runtime-deps:6.0

# Install Apache2 with proxy modules
RUN apt-get update \
    && apt-get install -y --no-install-recommends apache2 \
    && a2enmod proxy proxy_http headers \
    && apt-get clean \
    && rm -rf /var/lib/apt/lists/*

WORKDIR /app

# Copy published artefacts
COPY --from=build /publish ./

RUN chmod +x /app/XRCultureClientApp

# Apache virtual host (replaces the default site)
COPY Docker/apache/xrculture.conf /etc/apache2/sites-available/000-default.conf

# Entrypoint script
COPY Docker/entrypoint.sh /entrypoint.sh
RUN chmod +x /entrypoint.sh

# Resources is a writable volume (app reads/writes XML files and model downloads)
VOLUME /app/Resources

# Kestrel bound to loopback only — Apache is the public face
ENV ASPNETCORE_URLS=http://127.0.0.1:5000
ENV ASPNETCORE_ENVIRONMENT=Production
ENV ASPNETCORE_FORWARDEDHEADERS_ENABLED=true

EXPOSE 80

HEALTHCHECK --interval=30s --timeout=10s --start-period=15s --retries=3 \
    CMD curl -f http://localhost/ || exit 1

ENTRYPOINT ["/entrypoint.sh"]
