import type { XRHandState } from "@pmndrs/xr";
import type { XRControllerState } from "@react-three/xr";
import { ArrowHelper, LineBasicMaterial, Matrix4, Quaternion, Raycaster, Vector3, type Object3D, type Object3DEventMap, type Vector2 } from "three";

const POINTER_DEFAULT_LENGTH = 50;

/**
 * Raycast hit result with intersection data
 */
export type RaycastResult = {
    distance: number;
    point: Vector3;
    normal: Vector3 | undefined;
    object: Object3D<Object3DEventMap>;
    uv: Vector2 | undefined;
};

/**
 * Position and rotation offsets for object manipulation in controller/hand space
 */
export type TransformOffsets = {
    positionOffset: Vector3;
    rotationOffset: Quaternion;
}

// =================================================
// WebXR to Three.js Conversion Utilities
// =================================================

/**
 * Convert XRRigidTransform matrix to Three.js Matrix4
 */
export function xrMatrixToMatrix4(xrMatrix: Float32Array): Matrix4 {
    const matrix = new Matrix4();
    matrix.fromArray(xrMatrix);
    return matrix;
}

/**
 * Convert XRRigidTransform position to Three.js Vector3
 */
export function xrPositionToVector3(position: DOMPointReadOnly): Vector3 {
    return new Vector3(position.x, position.y, position.z);
}

/**
 * Convert XRRigidTransform orientation to Three.js Quaternion
 */
export function xrOrientationToQuaternion(orientation: DOMPointReadOnly): Quaternion {
    return new Quaternion(orientation.x, orientation.y, orientation.z, orientation.w);
}

/**
 * Raycast from controller/hand into scene
 * Returns closest intersection or null if no hit
 */
export function performRaycast(raycaster: Raycaster | null | undefined, transform: XRRigidTransform | null | undefined, object: Object3D | null | undefined): RaycastResult | null {
    if (!raycaster || !transform || !object) return null;

    const position = new Vector3(transform.position.x, transform.position.y, transform.position.z);
    const direction = new Vector3(0, 0, -1);
    direction.applyQuaternion(new Quaternion(transform.orientation.x, transform.orientation.y, transform.orientation.z, transform.orientation.w));

    raycaster.set(position, direction);
    const intersects = raycaster.intersectObject(object, true);

    if (intersects.length <= 0) return null;

    const hit = intersects[0];
    return {
        distance: hit.distance,
        point: hit.point,
        normal: hit.face?.normal,
        object: hit.object,
        uv: hit.uv
    };
}

/**
 * Get pose (position and orientation) of controller/hand
 */
export function evaluatePose(controller: XRControllerState | XRHandState | null | undefined, baseSpace: XRSpace | null | undefined, frame: XRFrame | null | undefined): XRPose | null | undefined {
    if (!controller?.inputSource.targetRaySpace || !baseSpace) return null;
    
    return frame?.getPose(controller.inputSource.targetRaySpace, baseSpace);
}

/**
 * Update arrow helper visual from controller/hand pose
 */
export function updateArrowHelper(arrow: ArrowHelper | null | undefined, controller: XRControllerState | XRHandState | null | undefined, baseSpace: XRSpace | null | undefined, frame: XRFrame | null | undefined) {
    if (!arrow || !baseSpace) return;

    const transform = evaluatePose(controller, baseSpace, frame)?.transform;
    if (!transform) return;

    const position = new Vector3(transform.position.x, transform.position.y, transform.position.z);
    const direction = new Vector3(0, 0, -1);
    const rotation = new Quaternion(transform.orientation.x, transform.orientation.y, transform.orientation.z, transform.orientation.w);
    direction.applyQuaternion(rotation);

    arrow.position.copy(position);
    arrow.setDirection(direction);
    // No arrowhead: hide the cone and let the line span the full length.
    arrow.cone.visible = false;
    arrow.setLength(POINTER_DEFAULT_LENGTH, 0, 0);
}


export function setArrowHelperHighlight(arrow: ArrowHelper | null | undefined, highlighted: boolean) {
    if (!arrow) return;

    arrow.setColor(highlighted ? 0xffcc00 : 0xffffff);

    const lineMat = arrow.line.material as LineBasicMaterial;
    lineMat.depthTest = !highlighted;
    lineMat.transparent = highlighted;

    arrow.line.renderOrder = highlighted ? 9999 : 0;
}


export type PoiHit = {
    index: number;
    distance: number;
};

/**
 * Raycast from a controller/hand pose into a group of POIs and return the
 * nearest POI sphere actually hit, or null. Non-POI objects are ignored.
 */
export function raycastPoi(raycaster: Raycaster | null | undefined, transform: XRRigidTransform | null | undefined, group: Object3D | null | undefined): PoiHit | null {
    if (!raycaster || !transform || !group) return null;

    const position = new Vector3(transform.position.x, transform.position.y, transform.position.z);
    const direction = new Vector3(0, 0, -1);
    direction.applyQuaternion(new Quaternion(transform.orientation.x, transform.orientation.y, transform.orientation.z, transform.orientation.w));

    raycaster.set(position, direction);
    const intersects = raycaster.intersectObject(group, true);

    for (const hit of intersects) {
        const poiIndex = hit.object.userData?.poiIndex;
        if (typeof poiIndex === "number") return { index: poiIndex, distance: hit.distance };
    }

    return null;
}

/**
 * Calculate position and rotation offsets between Object3D and grabbed object
 */
export function getTransformOffsets(inputSource: Object3D | null | undefined, object: Object3D | null | undefined): TransformOffsets | null {
    if (!inputSource || !object) return null;

    // Get object position in world space
    const modelWorldPos = new Vector3();
    object.getWorldPosition(modelWorldPos);

    // Transform to input source's local space
    const inverseControllerMatrix = inputSource.matrixWorld.clone().invert();
    const positionOffset = modelWorldPos.applyMatrix4(inverseControllerMatrix);

    // Get rotation offset
    const controllerQuaternion = new Quaternion();
    inputSource.getWorldQuaternion(controllerQuaternion);

    const modelQuaternion = new Quaternion();
    object.getWorldQuaternion(modelQuaternion);

    // Calculate relative rotation: offset = controller⁻¹ × model
    const rotationOffset = controllerQuaternion.invert().multiply(modelQuaternion);

    return { positionOffset, rotationOffset };
}

/**
 * Calculate position and rotation offsets between XRRigidTransform and grabbed object
 */
export function getTransformOffsetsFromXR(xrTransform: XRRigidTransform | null | undefined, object: Object3D | null | undefined): TransformOffsets | null {
    if (!xrTransform || !object) return null;

    // Get object position in world space
    const modelWorldPos = new Vector3();
    object.getWorldPosition(modelWorldPos);

    // Transform to XR input source's local space
    const inverseControllerMatrix = xrMatrixToMatrix4(xrTransform.matrix).invert();
    const positionOffset = modelWorldPos.applyMatrix4(inverseControllerMatrix);

    // Get rotation offset
    const controllerQuaternion = xrOrientationToQuaternion(xrTransform.orientation);
    const modelQuaternion = new Quaternion();
    object.getWorldQuaternion(modelQuaternion);

    // Calculate relative rotation: offset = controller⁻¹ × model
    const rotationOffset = controllerQuaternion.invert().multiply(modelQuaternion);

    return { positionOffset, rotationOffset };
}