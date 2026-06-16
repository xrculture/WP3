import { useRef, useState, useCallback } from "react";

import { Box3, FrontSide, MeshStandardMaterial, Vector3, type Object3D } from "three";

import { useFrame } from "@react-three/fiber";
import { Billboard, Text } from "@react-three/drei";

import type { PoiData } from "../hooks/useModel";

type Props = {
    poi: PoiData;
    index: number;
    hoveredPoiIndicesRef: { current: Set<number> };
};

const PANEL_WIDTH = 2.0;
const PANEL_HEIGHT = 1.0;
const PANEL_PADDING = 0.10;

const TITLE_FONT_SIZE_DEFAULT = 0.1;
const TITLE_FONT_SIZE_MIN = 0.05;
const DESC_FONT_SIZE_DEFAULT = 0.07;
const DESC_FONT_SIZE_MIN = 0.035;

const SPHERE_RENDER_ORDER = 999;
const PANEL_RENDER_ORDER = 1000;

const noRaycast = () => null;

export default function Poi({ poi, index, hoveredPoiIndicesRef }: Props) {
    const poiRef = useRef<Object3D>(null);
    const sphereMatRef = useRef<MeshStandardMaterial>(null);

    const [showInfo, setShowInfo] = useState(false);

    useFrame(() => {
        if (!sphereMatRef.current) return;
        const isHovered = hoveredPoiIndicesRef.current.has(index);
        if (showInfo) {
            sphereMatRef.current.color.set('#c8a96e');
        } else if (isHovered) {
            sphereMatRef.current.color.set('#66b3ff');
        } else {
            sphereMatRef.current.color.set('#3a86ff');
        }
    });
    const [titleFontSize, setTitleFontSize] = useState(TITLE_FONT_SIZE_DEFAULT);
    const [descriptionFontSize, setDescFontSize] = useState(DESC_FONT_SIZE_DEFAULT);
    const [titleHeight, setTitleHeight] = useState(0.14);

    const position = poi.position as [number, number, number];
    const title = poi.title as string;
    const description = poi.description as string;

    const maxContentWidth = PANEL_WIDTH - PANEL_PADDING * 2;

    const handleTitleSync = useCallback((mesh: any) => {
        if (!mesh || !mesh.material) return;
        mesh.material.side = FrontSide;
        mesh.material.depthTest = false;

        const box = new Box3();
        box.expandByObject(mesh);
        const size = box.getSize(new Vector3());

        const maxTitleHeight = PANEL_HEIGHT * 0.35;
        if ((size.x > maxContentWidth || size.y > maxTitleHeight) && titleFontSize > TITLE_FONT_SIZE_MIN) {
            setTitleFontSize((prev) => Math.max(prev - 0.005, TITLE_FONT_SIZE_MIN));
        } else {
            setTitleHeight(size.y > 0 ? size.y : 0.14);
        }
    }, [titleFontSize, maxContentWidth]);

    const handleDescriptionSync = useCallback((mesh: any) => {
        if (!mesh || !mesh.material) return;
        mesh.material.side = FrontSide;
        mesh.material.depthTest = false;

        const box = new Box3();
        box.expandByObject(mesh);
        const size = box.getSize(new Vector3());

        const descMaxHeight = PANEL_HEIGHT - PANEL_PADDING * 2 - titleHeight - 0.05 - 0.02;
        if ((size.x > maxContentWidth || size.y > descMaxHeight) && descriptionFontSize > DESC_FONT_SIZE_MIN) {
            setDescFontSize((prev) => Math.max(prev - 0.005, DESC_FONT_SIZE_MIN));
        }
    }, [descriptionFontSize, titleHeight, maxContentWidth]);

    const panelPosition: [number, number, number] = [
        position[0],
        position[1] + PANEL_HEIGHT + 0.3,
        position[2]
    ];

    const dividerY = PANEL_HEIGHT / 2 - PANEL_PADDING - titleHeight - 0.03;
    const descY = dividerY - 0.03;

    return (
        <>
            {/* Info panel - always mounted to avoid font loading flash */}
            <Billboard
                visible={showInfo}
                position={panelPosition}>
                {/* Border — rendered first so background paints over it */}
                <mesh
                    raycast={noRaycast}
                    renderOrder={PANEL_RENDER_ORDER}
                    position={[0, 0, -0.02]}>
                    <planeGeometry args={[PANEL_WIDTH + 0.04, PANEL_HEIGHT + 0.04]} />
                    <meshStandardMaterial color="#c8a96e" opacity={0.95} transparent depthTest={false} depthWrite={false} />
                </mesh>

                {/* Background panel */}
                <mesh
                    raycast={noRaycast}
                    renderOrder={PANEL_RENDER_ORDER}
                    position={[0, 0, -0.01]}>
                    <planeGeometry args={[PANEL_WIDTH, PANEL_HEIGHT]} />
                    <meshStandardMaterial color="#1a1a2e" opacity={0.92} transparent depthTest={false} depthWrite={false} />
                </mesh>

                {/* Title */}
                <Text
                    raycast={noRaycast}
                    renderOrder={PANEL_RENDER_ORDER}
                    onSync={handleTitleSync}
                    position={[0, PANEL_HEIGHT / 2 - PANEL_PADDING, 0]}
                    fontSize={titleFontSize}
                    maxWidth={maxContentWidth}
                    textAlign="center"
                    color="#c8a96e"
                    anchorY="top">
                    {title}
                </Text>

                {/* Divider line */}
                <mesh
                    raycast={noRaycast}
                    renderOrder={PANEL_RENDER_ORDER}
                    position={[0, dividerY, 0]}>
                    <planeGeometry args={[maxContentWidth, 0.005]} />
                    <meshStandardMaterial color="#c8a96e" opacity={0.5} transparent depthTest={false} depthWrite={false} />
                </mesh>

                {/* Description */}
                <Text
                    raycast={noRaycast}
                    renderOrder={PANEL_RENDER_ORDER}
                    onSync={handleDescriptionSync}
                    position={[-PANEL_WIDTH / 2 + PANEL_PADDING, descY, 0]}
                    fontSize={descriptionFontSize}
                    maxWidth={maxContentWidth}
                    textAlign="left"
                    color="#e8e8e8"
                    anchorX="left"
                    anchorY="top"
                    lineHeight={1.4}>
                    {description}
                </Text>
            </Billboard>

            {/* POI sphere */}
            <mesh
                ref={poiRef}
                renderOrder={SPHERE_RENDER_ORDER}
                position={position}
                userData={{ poiIndex: index }}
                onClick={() => setShowInfo(!showInfo)}>
                <sphereGeometry args={[0.15, 16, 16]} />
                <meshStandardMaterial ref={sphereMatRef} depthTest={false} depthWrite={false} />
            </mesh>
        </>
    );
}
