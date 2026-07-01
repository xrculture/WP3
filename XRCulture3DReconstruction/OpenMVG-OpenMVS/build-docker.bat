@echo off

echo Rebuilding Docker container...
docker build --no-cache -t openmvg-openmvs .
