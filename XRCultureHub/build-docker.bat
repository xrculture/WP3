@echo off
echo Publishing...
dotnet publish XRCultureHub/XRCultureHub.csproj -c Release -o XRCultureHub/bin/Release/net8.0/publish

echo Rebuilding and starting Docker container...
docker compose down
docker compose build --no-cache
docker compose up