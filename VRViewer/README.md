# XR-Culture

3D model viewer with WebXR (VR/AR) support, deployable via Docker.

## Requirements

- [Docker](https://www.docker.com/) and Docker Compose

## Start

```bash
cd backend
docker compose up -d --build
```

The viewer will be available at **http://localhost:5200**.

Open a 3D model directly in the browser:

```
http://localhost:5200?path=https://example.com/model.glb
```

## Stop

```bash
cd backend
docker compose down
```

---

## Adding Points of Interest (POI) to a glTF file

POIs are defined inside the `asset.extras.pois` array of a `.gltf` file.  
Each POI has a `title`, a `description`, and a `position` (x, y, z in model-space units).

```json
{
  "asset": {
    "version": "2.0",
    "extras": {
      "pois": [
        {
          "title": "Engine compartment",
          "description": "Main engine block. Maintenance interval: 500h.",
          "position": [0.0, 1.2, 0.5]
        },
        {
          "title": "Control panel",
          "description": "Primary interface for operator controls.",
          "position": [-0.8, 1.8, 0.0]
        }
      ]
    }
  }
}
```

> **Note:** POIs live in `asset.extras`, which is the standard glTF extension point for custom metadata — ignored by renderers that don't support them, so the file stays fully valid.

See "inject_extras.py" for an example on how to inject POI data on a glb file.