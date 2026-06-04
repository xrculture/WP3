import { createRoot } from "react-dom/client"

import "./index.css"
import css from "./index.css?inline";
import App from "./App.tsx"

// import { StrictMode } from "react";

// createRoot(document.getElementById("root")!).render(
//     <StrictMode>
//         <App
//             // path="https://raw.githubusercontent.com/mrdoob/three.js/dev/examples/models/obj/walt/WaltHead.obj" />
//             // path="https://raw.githubusercontent.com/mrdoob/three.js/dev/examples/models/obj/tree.obj" /> 
//             // path="https://raw.githubusercontent.com/mrdoob/three.js/dev/examples/models/fbx/Samba%20Dancing.fbx" /> 
//             // path="/Duck.gltf" />
//             path="https://raw.githubusercontent.com/KhronosGroup/glTF-Sample-Models/master/2.0/Duck/glTF-Binary/Duck.glb" />
//     </StrictMode>
// );

export type SceneConfig = {
    backgroundColor?: string;   // Hex color senza # (es. "1A1A2E")
    zoom?: number;              // Camera zoom/distance
    pan?: [number, number, number]; // Camera target position [x, y, z]
    lightIntensity?: number;    // Intensità luce (default 1)
};

class AppVR extends HTMLElement {
    shadow: ShadowRoot;

    constructor() {
        super();

        this.shadow = this.attachShadow({ mode: "open" });
    }

    connectedCallback() {
        // Inietta lo stile solo se non già presente
        if (!this.shadow.querySelector("style[data-injected]")) {
            const style = document.createElement("style");
            style.setAttribute("data-injected", "true");
            style.textContent = css;

            this.shadow.appendChild(style);
        }

        const params = new URLSearchParams(window.location.search);

        // Leggi l'attributo path dal custom element,
        // altrimenti fallback al query parameter ?path= della pagina ospitante
        const path =
            this.getAttribute("path") ??
            params.get("path") ??
            "";

        // Leggi SceneInit da attributi o query parameters
        const sceneConfig: SceneConfig = {};

        const bgColor = this.getAttribute("background-color") ?? params.get("bg");
        if (bgColor) sceneConfig.backgroundColor = bgColor;

        const zoom = this.getAttribute("zoom") ?? params.get("zoom");
        if (zoom) sceneConfig.zoom = parseFloat(zoom);

        const pan = this.getAttribute("pan") ?? params.get("pan");
        if (pan) {
            const parts = pan.split(",").map(Number);
            if (parts.length === 3 && parts.every(n => !isNaN(n))) {
                sceneConfig.pan = parts as [number, number, number];
            }
        }

        const lights = this.getAttribute("light-intensity") ?? params.get("lights");
        if (lights) sceneConfig.lightIntensity = parseFloat(lights);

        // Monta React nell'elemento shadow
        createRoot(this.shadow).render(<App path={path} sceneConfig={sceneConfig} />);
    }
}

customElements.define("app-vr", AppVR);