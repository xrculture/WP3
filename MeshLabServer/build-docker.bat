@echo off
echo Publishing...
dotnet publish MeshLabServer/MeshLabServer.csproj -c Release -o MeshLabServer/bin/Release/net9.0/publish

echo Rebuilding and starting Docker container...
docker compose down
docker compose build --no-cache
docker compose up