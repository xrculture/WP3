import { useEffect, useMemo } from "react";

import { useLoader, useThree } from "@react-three/fiber";
import { EquirectangularReflectionMapping, SRGBColorSpace, TextureLoader } from "three";

import skyboxImage from "../assets/skybox.jpg";

type Props = {
    show: boolean
};

export default function SkyBox({ show }: Props) {
    const loadedTexture = useLoader(TextureLoader, skyboxImage);

    const texture = useMemo(() => {
        const clonedTexture = loadedTexture.clone();
        clonedTexture.mapping = EquirectangularReflectionMapping;
        clonedTexture.colorSpace = SRGBColorSpace;
        clonedTexture.needsUpdate = true;
        return clonedTexture;
    }, [loadedTexture]);

    useThree(({ scene }) => scene.background = show ? texture : null);

    useEffect(() => {
        return () => texture.dispose();
    }, [texture]);

    return null;
}