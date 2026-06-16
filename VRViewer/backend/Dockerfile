# ─── Stage 1: Build the frontend web-component ────────────────────────────────
FROM node:20-alpine AS frontend-build

WORKDIR /app/frontend
COPY frontend/package*.json ./
RUN npm ci
COPY frontend/ ./
RUN npx cross-env BUILD_TARGET=lib vite build

# ─── Stage 2: Build the backend ───────────────────────────────────────────────
FROM node:20-alpine AS backend-build

WORKDIR /app/backend
COPY backend/package*.json ./
RUN npm ci
COPY backend/ ./
RUN npm run build

# ─── Stage 3: Production runtime ──────────────────────────────────────────────
FROM node:20-alpine AS production

WORKDIR /app

# openssl is needed by the entrypoint to generate the self-signed TLS cert
RUN apk add --no-cache openssl

# Install only production dependencies
COPY backend/package*.json ./
RUN npm ci --omit=dev

# Copy compiled backend
COPY --from=backend-build /app/backend/dist ./dist

# Copy frontend lib build into public/
COPY --from=frontend-build /app/frontend/dist ./public

# Copy the hosting HTML page
COPY backend/public/index.html ./public/index.html

# Create XML directory
RUN mkdir -p /xrculture/viewer/xml

# Environment
ENV PORT=8080
ENV XML_DIR=/xrculture/viewer/xml
ENV NODE_ENV=production

EXPOSE 8080

COPY backend/docker-entrypoint.sh ./docker-entrypoint.sh
RUN chmod +x ./docker-entrypoint.sh

CMD ["./docker-entrypoint.sh"]