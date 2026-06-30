#!/bin/sh
set -e

# Start Apache2 in background daemon mode
apache2ctl start

# Start Kestrel as the foreground process (PID 1).
# Container lifetime is tied to this process.
exec /app/XRCultureClientApp
