@echo off
echo Publishing...
dotnet publish XRCultureServices/XRCultureServices.csproj -c Release -o XRCultureServices/bin/Release/net9.0/publish

echo Rebuilding and starting Docker container...
docker compose down
docker compose build --no-cache
docker compose up