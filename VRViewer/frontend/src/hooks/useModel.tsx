import { Group, LoadingManager, type Object3DEventMap } from "three";

import { GLTFLoader } from "three/addons/loaders/GLTFLoader.js";
import { MTLLoader } from "three/addons/loaders/MTLLoader.js";
import { FBXLoader } from "three/addons/loaders/FBXLoader.js"
import { OBJLoader } from "three/addons/loaders/OBJLoader.js"
import { useEffect, useState } from "react";

type Props = {
    path: string
};

// The shape of your custom extras - define it however you want
export type Extras = {
    [key: string]: unknown;
};

export type PoiData = {
    title: string;
    description: string;
    position: [number, number, number];
};

export type ModelMetadata = {
    // // From asset.extras (global file metadata)
    // asset?: ModelExtras;
    // // From each node's extras, keyed by node name
    // nodes?: Record<string, ModelExtras>;
    // POIs extracted from asset.extras.pois
    pois?: PoiData[];
};

export default function useModel({ path }: Props) {
    const [progress, setProgress] = useState<number>(0);

    const [error, setError] = useState<string | Error>();

    const [model, setModel] = useState<Group<Object3DEventMap>>();

    const [metadata, setMetadata] = useState<ModelMetadata>();

    function onProgress(event: ProgressEvent<EventTarget>) {
        setProgress(event.loaded / event.total);
    }

    function normalizePath(value: string): string {
        return (value ?? "").trim();
    }

    // Cross-origin model URLs are often blocked by the remote host's CORS policy.
    // Route them through our own backend, which fetches them server-side (not
    // subject to browser CORS) and streams the bytes back same-origin.
    function toProxiedUrl(value: string): string {
        if (!value || value.startsWith("data:") || value.startsWith("blob:")) return value;

        try {
            const parsed = new URL(value, window.location.href);
            if (parsed.origin === window.location.origin) return value;
            if (parsed.protocol !== "http:" && parsed.protocol !== "https:") return value;

            return `/api/proxy?url=${encodeURIComponent(parsed.href)}`;
        } catch {
            return value;
        }
    }

    function extractExtension(value: string): string {
        const normalized = normalizePath(value);
        if (!normalized) return "";

        // Support data URLs, e.g. data:model/gltf-binary;base64,...
        if (normalized.startsWith("data:")) {
            const mimeType = normalized.slice(5, normalized.indexOf(";") > -1 ? normalized.indexOf(";") : normalized.length).toLowerCase();

            if (mimeType.includes("gltf-binary")) return ".glb";
            if (mimeType.includes("gltf+json")) return ".gltf";
            if (mimeType.includes("x-fbx") || mimeType.includes("fbx")) return ".fbx";
            if (mimeType.includes("wavefront-obj") || mimeType.endsWith("/obj")) return ".obj";

            return "";
        }

        const lowerPath = (() => {
            try {
                // Handles absolute and protocol-relative URLs while preserving plain relative paths.
                if (/^(https?:)?\/\//i.test(normalized)) {
                    const parsed = new URL(normalized, window.location.href);
                    return decodeURIComponent(parsed.pathname).toLowerCase();
                }
            } catch {
                // Fall through to best-effort string handling below.
            }

            return decodeURIComponent(normalized.split(/[?#]/)[0]).toLowerCase();
        })();

        if (lowerPath.endsWith(".gltf")) return ".gltf";
        if (lowerPath.endsWith(".glb")) return ".glb";
        if (lowerPath.endsWith(".fbx")) return ".fbx";
        if (lowerPath.endsWith(".obj")) return ".obj";

        return "";
    }

    useEffect(() => {
        async function downloadAsync() {
            try {
                const normalizedPath = normalizePath(path);
                const extension = extractExtension(normalizedPath);

                if (!normalizedPath) {
                    throw new Error("Model path is missing. Provide a non-empty path attribute or ?path= query parameter.");
                }

                let downloadedModel: Group<Object3DEventMap>

                const manager = new LoadingManager();
                manager.setURLModifier(toProxiedUrl);

                if (extension === ".gltf" || extension === ".glb") {
                    const loader = new GLTFLoader(manager);
                    const gltf = await loader.loadAsync(normalizedPath, onProgress);
                    downloadedModel = gltf.scene;

                    // // Extract extras from asset (global metadata)
                    // // and from each node (per-object metadata)
                    // const nodeExtras: Record<string, ModelExtras> = {};
                    // gltf.scene.traverse(obj => {
                    //     if (obj.userData && Object.keys(obj.userData).length > 0) {
                    //         nodeExtras[obj.name] = obj.userData as ModelExtras;
                    //     }
                    // });

                    const extras = (gltf.asset.extras ?? (gltf.parser.json as { extras?: Extras }).extras) as Extras | undefined;
                    const rawPois = extras?.pois;
                    const pois: PoiData[] | undefined = Array.isArray(rawPois)
                        ? (rawPois as PoiData[])
                        : undefined;

                    setMetadata({
                        // asset: assetExtras,
                        // nodes: Object.keys(nodeExtras).length > 0 ? nodeExtras : undefined,
                        pois,
                    });
                } else if (extension === ".fbx") {
                    const loader = new FBXLoader(manager);
                    downloadedModel = await loader.loadAsync(normalizedPath, onProgress);
                } else if (extension === ".obj") {
                    const mtlPath = normalizedPath.replace(/\.obj(?=$|[?#])/i, ".mtl");
                    const objLoader = new OBJLoader(manager);

                    // Try to load materials - gracefully skip if .mtl doesn't exist
                    try {
                        const mtlLoader = new MTLLoader(manager);

                        const materials = await mtlLoader.loadAsync(mtlPath);
                        materials.preload();
                        
                        objLoader.setMaterials(materials);
                    } catch {
                        console.warn("No .mtl file found, loading .obj without materials");
                    }

                    downloadedModel = await objLoader.loadAsync(normalizedPath, onProgress);
                } else {
                    throw new Error(`Path extension not supported: ${normalizedPath}`);
                }

                setModel(downloadedModel);
            } catch (error) {
                if (typeof error === "string") {
                    setError(error);
                } else if (error instanceof Error) {
                    setError(error);
                }
            }
        }

        downloadAsync();
    }, [path]);

    return {
        progress,
        error,
        model,
        metadata
    };
}