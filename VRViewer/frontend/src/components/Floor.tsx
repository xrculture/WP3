import type { Vector3 } from "three";

type Props = {
    position?: number | Vector3 | [x: number, y: number, z: number] | readonly [x: number, y: number, z: number] | Readonly<Vector3> | undefined;
    width: number;
    length: number;
    color: string;
    show: boolean;
};

export default function Floor({ position, width, length, color, show }: Props) {
    if (!show) return null;

    return (
        <mesh
            rotation={[-Math.PI / 2, 0, 0]}
            position={position}>
            <planeGeometry
                args={[width, length]} />

            <meshStandardMaterial
                color={color}
                // map={texture} // Load your floor texture
                transparent
                opacity={1}
            />
        </mesh>
    );
}