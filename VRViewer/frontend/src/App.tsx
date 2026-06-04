import { useEffect, useState } from "react";

import { Canvas } from "@react-three/fiber";
import { OrbitControls } from "@react-three/drei";
import { createXRStore, XR, XROrigin } from "@react-three/xr";

import { useModel } from "./hooks";

import { Floor, Skybox, Model, TestModel, CircularProgressBar, Spinner } from "./components";

import type { SceneConfig } from "./main";

const isTesting = false;
// const showDebug = false;

type Props = {
    path: string;
    sceneConfig?: SceneConfig;
};

const store = createXRStore({
    hand: true,
});

export default function App({ path, sceneConfig }: Props) {
    const { progress, error: modelError, model, metadata } = useModel({ path });

    const [isLoading, setIsLoading] = useState(false);

    // const [sessionType, setSessionType] = useState<string>("None");

    const [isInVR, setIsInVR] = useState(false);
    const [isInAR, setIsInAR] = useState(false);

    const [error, setError] = useState<string>();

    const [isVRSupported, setIsVRSupported] = useState(false);
    const [isARSupported, setIsARSupported] = useState(false);

    useEffect(() => {
        async function checkXRSupport() {
            if (!navigator.xr) return;

            const [vr, ar] = await Promise.all([
                navigator.xr.isSessionSupported("immersive-vr"),
                navigator.xr.isSessionSupported("immersive-ar"),
            ]);

            setIsVRSupported(vr);
            setIsARSupported(ar);
        }

        checkXRSupport();
    }, []);

    useEffect(() => {
        if (error) console.log("Generic error:", error);

        if (modelError) console.log("Model loading error:", modelError);
    }, [error, modelError]);

    store.subscribe((state) => {
        if (state.mode == "immersive-vr") {
            // setSessionType("VR");
            setIsInVR(true);
            setIsInAR(false);
        } else if (state.mode == "immersive-ar") {
            // setSessionType("AR");
            setIsInVR(false);
            setIsInAR(true);
        } else {
            // setSessionType("None");
            setIsInVR(false);
            setIsInAR(false);
        }

        if (state.session) setError("");
    });

    async function enterVRAsync() {
        try {
            setIsLoading(true);

            if (!await store.enterVR()) setError("VR is not available");
        } catch (error) {
            if (typeof error === "string") {
                setError(error);
            } else if (error instanceof Error) {
                setError(error.message);
            }

            console.error("Error entering VR:", error);
        } finally {
            setIsLoading(false);
        }
    }

    async function enterARAsync() {
        try {
            setIsLoading(true);

            if (!await store.enterAR()) setError("AR is not available");
        } catch (error) {
            if (typeof error === "string") {
                setError(error);
            } else if (error instanceof Error) {
                setError(error.message);
            }

            console.error("Error entering in AR:", error);
        } finally {
            setIsLoading(false);
        }
    }

    // Calcola il colore di sfondo dal sceneConfig
    const bgColor = sceneConfig?.backgroundColor
        ? `#${sceneConfig.backgroundColor}`
        : undefined;

    const lightIntensity = sceneConfig?.lightIntensity ?? 1;

    return (
        <div
            className="w-screen h-screen"
            style={bgColor ? { backgroundColor: bgColor } : undefined}>
            {model &&
                <>
                    <Canvas
                        style={bgColor ? { background: bgColor } : undefined}
                        camera={sceneConfig?.zoom ? { position: [0, 0, sceneConfig.zoom] } : undefined}>
                        <XR
                            store={store}>
                            {bgColor && <color attach="background" args={[bgColor]} />}

                            <ambientLight
                                intensity={lightIntensity} />

                            <directionalLight
                                position={[5, 5, 5]}
                                intensity={lightIntensity} />

                            <XROrigin>
                                {isTesting &&
                                    <TestModel
                                        model={model}
                                        metadata={metadata} />
                                }

                                {!isTesting &&
                                    <Model
                                        model={model}
                                        metadata={metadata} />
                                }
                            </XROrigin>

                            <Skybox
                                show={isInVR} />

                            <Floor
                                position={[0, 0, 0]}
                                width={1000}
                                length={1000}
                                color="#FFFFFF"
                                show={isInVR} />

                            {!isInVR && !isInAR &&
                                <OrbitControls
                                    target={sceneConfig?.pan ?? [0, 0, 0]} />
                            }
                        </XR>
                    </Canvas>

                    {!isInVR && !isInAR &&
                        <div
                            className="absolute left-0 right-0 top0 bottom-5 justify-center items-center flex gap-3">
                            {isVRSupported && <button
                                className="px-4 py-2 text-white bg-blue-500 rounded hover:bg-blue-600"
                                onClick={enterVRAsync}>
                                Enter VR
                            </button>}

                            {isARSupported && <button
                                className="px-4 py-2 text-white bg-green-500 rounded hover:bg-green-600"
                                onClick={enterARAsync}>
                                Enter AR
                            </button>}
                        </div>
                    }
                </>
            }

            {!model &&
                <CircularProgressBar
                    percentage={progress * 100}
                    color="black" />
            }

            {isLoading &&
                <Spinner
                    color="#000000" />
            }
        </div>
    )
}