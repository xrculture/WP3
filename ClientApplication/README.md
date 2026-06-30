# XRCulture Middleware Demo

Reference client implementation of the [XRCulture Middleware protocol](https://github.com/peterrdf/xrculture) (D3.1). Built with ASP.NET Core 6 MVC.

## What it does

The application is composed of three independent sub-applications sharing a single deployment:

| Sub-application | Role |
|---|---|
| **Custom UI** | Web frontend — search, browse results, select file, dispatch to viewer |
| **Repository connectors** | Europeana, Zenodo, Amazon S3 — search and retrieve 3D models |
| **Viewer connector** | Queries the XRCulture Hub registry, filters viewers by format, posts `ModelLoading` requests |

### Supported 3D formats

`.obj` `.ifc` `.dae` `.glb`

## Architecture

```
Browser
  └─ HomeController (MVC)
       ├─ BridgeService      → Europeana Search API
       ├─ ZenodoService      → Zenodo Records API
       ├─ S3Service          → Amazon S3 / MinIO
       └─ ViewerService      → Hub Registry + ModelLoading dispatch

ConnectorController (REST API — /api/connector/*)
       ├─ POST search-europeana   accepts/returns ModelRequest/ModelResponse XML
       ├─ POST search-zenodo
       ├─ POST search-amazon
       └─ POST upload             multipart: XML manifest + optional file → Zenodo or S3
```

The search connectors all produce `ModelResponse` XML (the protocol message defined in D3.1), saved locally under `Resources/` for inspection.

## Configuration

Edit `appsettings.json`:

```json
{
  "Options": {
    "EuropeanaApiKey": "<your-europeana-api-key>",
    "SupportedFormats": "obj|ifc|dae|glb"
  },
  "AWS": {
    "Region": "eu-west-1",
    "AccessKey": "<aws-access-key>",
    "SecretKey": "<aws-secret-key>"
  },
  "Viewers": {
    "Source": "remote",
    "RemoteEndpoint": "http://<hub-host>/Registry?Accept=application/xml&SessionToken=<token>&ServiceType=Viewer"
  }
}
```

Set `Viewers.Source` to `local` to use the bundled `Resources/Viewers.xml` instead of the live registry.

## Run

```bash
dotnet run
```

Requires .NET 6 SDK. The app listens on the ports configured in `Properties/launchSettings.json`.

## Docker

The Docker setup lives in the [`Docker/`](Docker/) folder. It builds a self-contained linux-x64 image with **Apache 2** as reverse proxy and **Kestrel** as the application server.

### Quick start

```bash
# 1. Copy the environment template
cp Docker/.env.example Docker/.env

# 2. Fill in your credentials
#    EUROPEANA_API_KEY, AWS_ACCESS_KEY, AWS_SECRET_KEY, VIEWERS_REMOTE_ENDPOINT
$EDITOR Docker/.env

# 3. Build and start
cd Docker
docker compose up -d
```

The app is then available at <http://localhost>.

### Environment variables

All sensitive settings are passed as environment variables, which override the values in `appsettings.json`.

| Variable | appsettings.json key | Description |
|---|---|---|
| `EUROPEANA_API_KEY` | `Options.EuropeanaApiKey` | Europeana REST API key |
| `AWS_REGION` | `AWS.Region` | AWS region (default: `eu-west-1`) |
| `AWS_ACCESS_KEY` | `AWS.AccessKey` | AWS access key ID |
| `AWS_SECRET_KEY` | `AWS.SecretKey` | AWS secret access key |
| `VIEWERS_SOURCE` | `Viewers.Source` | `remote` or `local` (default: `remote`) |
| `VIEWERS_REMOTE_ENDPOINT` | `Viewers.RemoteEndpoint` | Hub registry URL with session token |

### Persistent volume

The application reads and writes files under `Resources/` (XML protocol messages, downloaded models). This directory is declared as a Docker volume so data survives container restarts:

```bash
# Named volume (managed by Docker — default)
docker compose up -d

# Bind mount to a host path instead
docker run -v /host/path/resources:/app/Resources ghcr.io/<org>/<repo>:latest
```

### Build locally

```bash
# From the repository root
docker build -f Docker/Dockerfile -t xrculture-middleware .
docker run -p 80:80 \
  -e Options__EuropeanaApiKey=<key> \
  -e AWS__Region=eu-west-1 \
  -e AWS__AccessKey=<key> \
  -e AWS__SecretKey=<secret> \
  -e Viewers__Source=remote \
  -e Viewers__RemoteEndpoint=<url> \
  -v xrculture_resources:/app/Resources \
  xrculture-middleware
```

### Pull from GitHub Container Registry

```bash
docker pull ghcr.io/xrculture/wp3:latest
```

The image is published automatically on every push to `main`/`master` and on version tags (`v*`) via the GitHub Actions workflow at [`.github/workflows/docker-publish.yml`](.github/workflows/docker-publish.yml).

### Container internals

| Component | Details |
|---|---|
| Base image | `mcr.microsoft.com/dotnet/runtime-deps:6.0` (Debian slim) |
| Web server | Apache 2 on port 80 (reverse proxy) |
| App server | Kestrel on `127.0.0.1:5000` (not exposed externally) |
| Volume | `/app/Resources` |
| Health check | `GET http://localhost/` every 30 s |

## API endpoints

All endpoints accept and return the XRCulture protocol XML messages.

| Method | Path | Input | Output |
|---|---|---|---|
| POST | `/api/connector/search-europeana` | `ModelRequest` XML (plain text) | `ModelResponse` XML |
| POST | `/api/connector/search-zenodo` | `ModelRequest` XML (plain text) | `ModelResponse` XML |
| POST | `/api/connector/search-amazon` | `ModelRequest` XML (plain text) | `ModelResponse` XML |
| POST | `/api/connector/upload` | multipart: `xmlRequest` field + optional `file` part | `ModelUploadResponse` XML |

The upload endpoint routes to Zenodo or S3 based on the `TargetRepository/ServiceID` element (`zenodo` or `s3`). A source URL may be supplied instead of a file part.

## Project context

Part of the XRCulture project (Digital Europe, Grant Agreement 101174317). Documented in deliverable D3.2 — *Web viewers and tools compliant with the XRCulture Middleware protocol*.
