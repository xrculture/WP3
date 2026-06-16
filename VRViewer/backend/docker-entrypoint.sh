#!/bin/sh
set -e

KEY_PATH="${SSL_KEY:-/app/key.pem}"
CERT_PATH="${SSL_CERT:-/app/cert.pem}"

if [ ! -f "$KEY_PATH" ] || [ ! -f "$CERT_PATH" ]; then
    # Build SAN list — always include localhost; add EXTERNAL_IP if provided
    SAN="DNS:localhost,IP:127.0.0.1"
    if [ -n "${EXTERNAL_IP:-}" ]; then
        SAN="${SAN},IP:${EXTERNAL_IP}"
    fi

    openssl req -x509 -newkey rsa:2048 \
        -keyout "$KEY_PATH" -out "$CERT_PATH" \
        -days 3650 -nodes \
        -subj "/O=XRCulture/CN=localhost" \
        -addext "subjectAltName=${SAN}" \
        2>/dev/null

    echo "Generated self-signed TLS certificate (SAN: ${SAN})"
fi

exec node dist/index.js
