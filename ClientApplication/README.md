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
