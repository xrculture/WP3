@echo off
echo Publishing...
dotnet publish XRCultureViewer/XRCultureViewer.csproj -c Release -r linux-x64 --self-contained false -o XRCultureViewer/bin/Release/net9.0/publish

echo Rebuilding and starting Docker container...
docker compose down
docker compose build --no-cache
docker compose up