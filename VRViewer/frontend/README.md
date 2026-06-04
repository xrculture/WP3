# XR-Culture Frontend

Web component React per la visualizzazione di modelli 3D con supporto WebXR (VR/AR).

## Tecnologie

- **React** 19 — UI library
- **TypeScript** — Linguaggio tipizzato
- **Three.js** 0.175 — Rendering 3D
- **@react-three/fiber** — React renderer per Three.js
- **@react-three/drei** — Helper e componenti utili
- **@react-three/xr** — Supporto WebXR (VR/AR)
- **Vite** — Build tool
- **Tailwind CSS** — Utility-first CSS framework
- **Zustand** — State management
- **Leva** — GUI per debug parametri

## Come funziona

Il frontend viene compilato come **web component** (`<app-vr>`) in un singolo file IIFE (`app-vr.iife.js`) che include tutto: React, Three.js, CSS. Può essere usato in qualsiasi pagina HTML senza dipendenze esterne.

### Attributi del web component

```html
<app-vr
    path="https://example.com/model.glb"
    background-color="1A1A2E"
    zoom="2.5"
    pan="0,1,0"
    light-intensity="0.8" />
```

| Attributo | Descrizione | Esempio |
|-----------|-------------|---------|
| `path` | URL del modello 3D (.glb, .gltf, .obj, .fbx) | URL remoto o path locale |
| `background-color` | Colore sfondo (hex senza #) | `1A1A2E` |
| `zoom` | Distanza camera | `2.5` |
| `pan` | Target camera (x,y,z) | `0,1,0` |
| `light-intensity` | Intensità luci (0.0 - 2.0) | `0.8` |

Il web component legge anche i **query parameters** dall'URL (`?path=...&bg=...&zoom=...`), utilizzati quando è hostato dal backend.

### Formati supportati

- `.glb` / `.gltf` (glTF)
- `.obj` (Wavefront OBJ)
- `.fbx` (Autodesk FBX)

---

## Sviluppo locale

```bash
cd frontend
npm install
npm run dev
```

Il dev server sarà su **https://localhost:5174** (HTTPS con certificato self-signed per WebXR).

---

## Build del web component

```bash
cd frontend
npm run build:lib
```

Produce `dist/app-vr.iife.js` — il bundle IIFE autoeseguibile.

---

## Deploy in Docker

Il frontend viene buildato automaticamente nel **Dockerfile multi-stage** del backend. Non serve un Dockerfile separato.

```bash
cd backend
docker compose up -d --build
```

Il Dockerfile:
1. Installa le dipendenze frontend e builda il web component con `npx cross-env BUILD_TARGET=lib vite build`
2. Copia `app-vr.iife.js` nella cartella `public/` del backend
3. Il backend serve tutto staticamente

Il viewer sarà disponibile su **http://localhost:5200**.

Per visualizzare un modello:
```
http://localhost:5200?path=https://example.com/model.glb
```

---

## Struttura del progetto

```
frontend/
├── src/
│   ├── main.tsx          ← Entry point + definizione web component <app-vr>
│   ├── App.tsx           ← Componente principale (Canvas 3D + XR)
│   ├── components/       ← Componenti 3D (Model, Floor, Skybox, ecc.)
│   ├── hooks/            ← Custom hooks (useModel, useControllers, ecc.)
│   └── utils/            ← Utility functions
├── vite.config.ts        ← Configurazione Vite (dev + lib build)
└── package.json
```
