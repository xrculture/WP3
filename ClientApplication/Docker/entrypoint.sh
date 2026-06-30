#!/bin/sh
set -e

# Start Apache2 in background daemon mode
apache2ctl start

# Start Kestrel as the foreground process (PID 1).
exec dotnet /app/XRCultureClientApp.dll
