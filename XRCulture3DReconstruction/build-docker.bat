@echo off
echo Publishing...
dotnet publish XRCulture3DReconstruction/XRCulture3DReconstruction.csproj -c Release -r linux-x64 --self-contained false -o XRCulture3DReconstruction/bin/Release/net8.0/publish

echo Rebuilding and starting Docker container...
docker compose down
docker compose build --no-cache
docker compose up