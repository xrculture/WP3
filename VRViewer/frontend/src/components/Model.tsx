import { useEffect, useRef } from "react";

import { Group, Object3D, Quaternion, Vector3, type Object3DEventMap } from "three";

import { useFrame } from "@react-three/fiber";
import { useXR, type XRControllerState } from "@react-three/xr";

import type { XRHandState } from "@pmndrs/xr";

import { useControllers, useHands } from "../hooks";
import type { ModelMetadata } from "../hooks/useModel";

import { getTransformOffsetsFromXR, performRaycast, evaluatePose, updateArrowHelper, xrMatrixToMatrix4, xrOrientationToQuaternion, xrPositionToVector3 } from "../utils";

import Poi from "./Poi";

// // Scale factor based on hand orientation (pinch gesture)
// function calculateRotationScaleFactor(
//     firstTransform: XRRigidTransform,
//     secondTransform: XRRigidTransform,
//     initialFirstOrientation: Quaternion,
//     initialSecondOrientation: Quaternion
// ): number {
//     const firstQuaternion = xrOrientationToQuaternion(firstTransform.orientation);
//     const secondQuaternion = xrOrientationToQuaternion(secondTransform.orientation);

//     const firstRelativeRotation = firstQuaternion.clone().multiply(initialFirstOrientation.clone().invert());
//     const secondRelativeRotation = secondQuaternion.clone().multiply(initialSecondOrientation.clone().invert());

//     // Check hand orientation relative to connection axis
//     const forward = new Vector3(0, 0, -1);
//     const firstForward = forward.clone().applyQuaternion(firstRelativeRotation);
//     const secondForward = forward.clone().applyQuaternion(secondRelativeRotation);

//     const firstPos = xrPositionToVector3(firstTransform.position);
//     const secondPos = xrPositionToVector3(secondTransform.position);
//     const connectionAxis = secondPos.clone().sub(firstPos).normalize();

//     // Project forward vectors onto perpendicular plane
//     const firstProjected = firstForward.clone().sub(connectionAxis.clone().multiplyScalar(firstForward.dot(connectionAxis)));
//     const secondProjected = secondForward.clone().sub(connectionAxis.clone().multiplyScalar(secondForward.dot(connectionAxis)));

//     if (firstProjected.length() > 0.001) firstProjected.normalize();
//     if (secondProjected.length() > 0.001) secondProjected.normalize();

//     // Determine if hands are pinching (pointing towards each other)
//     const firstTowardsCenter = firstForward.dot(connectionAxis);
//     const secondTowardsCenter = secondForward.dot(connectionAxis.clone().negate());
//     const inwardFactor = (firstTowardsCenter + secondTowardsCenter) / 2;
//     const rotationScale = 1 - (inwardFactor * 10);

//     return rotationScale < 0.1 ? 0.1 : rotationScale;
// }

// // Calculate rotation from two-hand movement
// function calculateTwoHandRotation(
//     firstPosition: Vector3,
//     secondPosition: Vector3,
//     initialConnectionAxis: Vector3,
//     initialUpVector: Vector3,
//     initialModelRotation: Quaternion
// ): Quaternion {
//     const currentConnectionAxis = secondPosition.clone().sub(firstPosition).normalize();
//     const axisRotation = new Quaternion().setFromUnitVectors(initialConnectionAxis, currentConnectionAxis);

//     const rotatedInitialUp = initialUpVector.clone().applyQuaternion(axisRotation);

//     const worldUp = new Vector3(0, 1, 0);
//     const currentUp = worldUp.clone()
//         .sub(currentConnectionAxis.clone().multiplyScalar(worldUp.dot(currentConnectionAxis)))
//         .normalize();

//     if (currentUp.length() < 0.001 || rotatedInitialUp.length() < 0.001) {
//         return axisRotation.clone().multiply(initialModelRotation);
//     }

//     // Twist rotation around connection axis
//     const twistRotation = new Quaternion().setFromUnitVectors(
//         rotatedInitialUp.normalize(),
//         currentUp.normalize()
//     );

//     const totalRotation = twistRotation.multiply(axisRotation);
//     return totalRotation.multiply(initialModelRotation);
// }

// XR frame data: raycast results, input states, and object transforms
// export type FrameResult = {
//     leftControllerRaycastResult: RaycastResult | null | undefined;
//     rightControllerRaycastResult: RaycastResult | null | undefined;
//     leftControllerTriggerIsPressed: boolean;
//     rightControllerTriggerIsPressed: boolean;
//     objectPosition: Vector3;
//     objectRotation: Quaternion;
//     lockPositionOffset: Vector3 | null | undefined;
//     lockRotationOffset: Quaternion | null | undefined;
// };

type XRStateTransform = {
    state: XRControllerState | XRHandState | null | undefined;
    transform: XRRigidTransform | null | undefined;
    hitPoint?: Vector3;
};

type Props = {
    // path: string;
    // onFrame: (result: FrameResult) => void;
    model: Group<Object3DEventMap>,
    metadata: ModelMetadata | undefined
};


export default function Model({ model, metadata }: Props) {
    const { session, originReferenceSpace } = useXR();

    const {
        leftController,
        rightController,
        leftControllerTransformRef,
        rightControllerTransformRef,
        leftControllerRaycastResultRef,
        rightControllerRaycastResultRef,
        leftControllerArrowHelperRef,
        rightControllerArrowHelperRef,
        isTriggerPressed,
    } = useControllers();

    const {
        leftHand,
        rightHand,
        leftHandTransformRef,
        rightHandTransformRef,
        leftHandRaycastResultRef,
        rightHandRaycastResultRef,
        leftHandArrowHelperRef,
        rightHandArrowHelperRef,
        isPinching
    } = useHands();

    const rootRef = useRef<Object3D>(null);
    const meshRef = useRef<Object3D>(null);

    const objectPositionRef = useRef(new Vector3());
    const objectRotationRef = useRef(new Quaternion());

    const lockPositionOffsetRef = useRef<Vector3>(null);
    const lockRotationOffsetRef = useRef<Quaternion>(null);
    const lockScaleRef = useRef(new Vector3());
    const lockModelPositionRef = useRef(new Vector3());
    const lockPivotPointRef = useRef(new Vector3());
    const lockPivotLocalRef = useRef(new Vector3());

    // Two-hand mode state
    const lockModelRotationRef = useRef(new Quaternion());
    const lockFirstOrientationRef = useRef(new Quaternion());
    const lockSecondOrientationRef = useRef(new Quaternion());
    const lockConnectionAxisRef = useRef(new Vector3());
    const lockUpVectorRef = useRef(new Vector3());

    const statesDistanceRef = useRef(0);
    const activeStatesTransformsRef = useRef<XRStateTransform[]>([]);

    useEffect(() => {
        objectPositionRef.current = new Vector3();
        objectRotationRef.current = new Quaternion();

        lockPositionOffsetRef.current = lockRotationOffsetRef.current = null;

        activeStatesTransformsRef.current = [];

        if (session) {
            rootRef.current?.position.set(0, 0, -5);
            // rootRef.current?.scale.set(4, 4, 4);
        }
        else {
            rootRef.current?.position.set(0, 0, 0);
            // rootRef.current?.scale.set(4, 4, 4);
        }

        rootRef.current?.quaternion.set(0, 0, 0, 1);
        rootRef.current?.scale.set(1, 1, 1);
        // rootRef.current?.scale.set(1, 1, 1);

    }, [session]);

    useFrame(({ raycaster }, _, frame) => {
        if (!model || !rootRef.current || !session || (!leftController && !rightController && !leftHand && !rightHand)) return;

        updateArrowHelper(leftControllerArrowHelperRef.current, leftController, originReferenceSpace, frame);
        updateArrowHelper(rightControllerArrowHelperRef.current, rightController, originReferenceSpace, frame);

        updateArrowHelper(leftHandArrowHelperRef.current, leftHand, originReferenceSpace, frame);
        updateArrowHelper(rightHandArrowHelperRef.current, rightHand, originReferenceSpace, frame);

        const leftControllerTriggerIsPressed = isTriggerPressed(leftController);
        const rightControllerTriggerIsPressed = isTriggerPressed(rightController);

        const leftHandIsPinching = isPinching(leftHand, originReferenceSpace, frame);
        const rightHandIsPinching = isPinching(rightHand, originReferenceSpace, frame);

        leftControllerRaycastResultRef.current = null;
        rightControllerRaycastResultRef.current = null;

        leftHandRaycastResultRef.current = null;
        rightHandRaycastResultRef.current = null;

        if (leftControllerTriggerIsPressed) {
            leftControllerTransformRef.current = evaluatePose(leftController, originReferenceSpace, frame)?.transform;

            leftControllerRaycastResultRef.current = performRaycast(raycaster, leftControllerTransformRef.current, rootRef.current);
        }

        if (rightControllerTriggerIsPressed) {
            rightControllerTransformRef.current = evaluatePose(rightController, originReferenceSpace, frame)?.transform;

            rightControllerRaycastResultRef.current = performRaycast(raycaster, rightControllerTransformRef.current, rootRef.current);
        }

        if (leftHandIsPinching) {
            leftHandTransformRef.current = evaluatePose(leftHand, originReferenceSpace, frame)?.transform;

            leftHandRaycastResultRef.current = performRaycast(raycaster, leftHandTransformRef.current, rootRef.current);
        }

        if (rightHandIsPinching) {
            rightHandTransformRef.current = evaluatePose(rightHand, originReferenceSpace, frame)?.transform;

            rightHandRaycastResultRef.current = performRaycast(raycaster, rightHandTransformRef.current, rootRef.current);
        }

        let reset = false;

        const existingLeftController = activeStatesTransformsRef.current.find(i => i.state === leftController);
        const existingRightController = activeStatesTransformsRef.current.find(i => i.state === rightController);

        const existingLeftHand = activeStatesTransformsRef.current.find(i => i.state === leftHand);
        const existingRightHand = activeStatesTransformsRef.current.find(i => i.state === rightHand);

        if (leftControllerTriggerIsPressed) {
            if (leftControllerRaycastResultRef.current) {
                if (!existingLeftController) activeStatesTransformsRef.current.push({ state: leftController, transform: leftControllerTransformRef.current, hitPoint: leftControllerRaycastResultRef.current.point.clone() });
                else existingLeftController.transform = leftControllerTransformRef.current;
            } else if (existingLeftController) {
                existingLeftController.transform = leftControllerTransformRef.current;
            }
        } else if (existingLeftController) {
            const currentInputSource = activeStatesTransformsRef.current[0];

            const index = activeStatesTransformsRef.current.indexOf(existingLeftController);
            activeStatesTransformsRef.current.splice(index, 1);

            reset = currentInputSource.state === leftController;
        }

        if (rightControllerTriggerIsPressed) {
            if (rightControllerRaycastResultRef.current) {
                if (!existingRightController) activeStatesTransformsRef.current.push({ state: rightController, transform: rightControllerTransformRef.current, hitPoint: rightControllerRaycastResultRef.current.point.clone() });
                else existingRightController.transform = rightControllerTransformRef.current;
            } else if (existingRightController) {
                existingRightController.transform = rightControllerTransformRef.current;
            }
        } else if (existingRightController) {
            const currentInputSource = activeStatesTransformsRef.current[0];

            const index = activeStatesTransformsRef.current.indexOf(existingRightController);
            activeStatesTransformsRef.current.splice(index, 1);

            reset = currentInputSource.state === rightController;
        }

        if (leftHandIsPinching) {
            if (leftHandRaycastResultRef.current) {
                if (!existingLeftHand) activeStatesTransformsRef.current.push({ state: leftHand, transform: leftHandTransformRef.current, hitPoint: leftHandRaycastResultRef.current.point.clone() });
                else existingLeftHand.transform = leftHandTransformRef.current;
            } else if (existingLeftHand) {
                existingLeftHand.transform = leftHandTransformRef.current;
            }
        } else if (existingLeftHand) {
            const currentInputSource = activeStatesTransformsRef.current[0];

            const index = activeStatesTransformsRef.current.indexOf(existingLeftHand);
            activeStatesTransformsRef.current.splice(index, 1);

            reset = currentInputSource.state === leftHand;
        }

        if (rightHandIsPinching) {
            if (rightHandRaycastResultRef.current) {
                if (!existingRightHand) activeStatesTransformsRef.current.push({ state: rightHand, transform: rightHandTransformRef.current, hitPoint: rightHandRaycastResultRef.current.point.clone() });
                else existingRightHand.transform = rightHandTransformRef.current;
            } else if (existingRightHand) {
                existingRightHand.transform = rightHandTransformRef.current;
            }
        } else if (existingRightHand) {
            const currentInputSource = activeStatesTransformsRef.current[0];

            const index = activeStatesTransformsRef.current.indexOf(existingRightHand);
            activeStatesTransformsRef.current.splice(index, 1);

            reset = currentInputSource.state === rightHand;
        }

        reset = reset || activeStatesTransformsRef.current.length === 0;

        // Also reset when going from 2 inputs to 1 (or 0)
        const wasScaling = statesDistanceRef.current !== 0;
        const isNowScaling = activeStatesTransformsRef.current.length >= 2;

        if (wasScaling && !isNowScaling) {
            // Exiting scale mode - bake meshRef offset into rootRef position
            statesDistanceRef.current = 0;
            if (meshRef.current && rootRef.current) {
                const scaledOffset = meshRef.current.position.clone().multiply(rootRef.current.scale);
                scaledOffset.applyQuaternion(rootRef.current.quaternion);
                rootRef.current.position.add(scaledOffset);
                meshRef.current.position.set(0, 0, 0);
            }
            // Force recalculation of drag offsets
            lockPositionOffsetRef.current = null;
            lockRotationOffsetRef.current = null;
        }

        if (reset) {
            lockPositionOffsetRef.current = null;
            lockRotationOffsetRef.current = null;
            statesDistanceRef.current = 0;
            if (meshRef.current && rootRef.current) {
                // Bake meshRef offset into rootRef position before resetting
                const scaledOffset = meshRef.current.position.clone().multiply(rootRef.current.scale);
                scaledOffset.applyQuaternion(rootRef.current.quaternion);
                rootRef.current.position.add(scaledOffset);
                meshRef.current.position.set(0, 0, 0);
            }
        } else {
            const firstInputSource = activeStatesTransformsRef.current[0];
            const secondInputSource = activeStatesTransformsRef.current[1]; // TODO: implement 2 controllers/hands drag/scale/rotation

            // Drag one controller/hand
            if (firstInputSource && !secondInputSource) {
                if (!lockPositionOffsetRef.current && !lockRotationOffsetRef.current) {
                    const offsets = getTransformOffsetsFromXR(firstInputSource.transform, rootRef.current);
                    if (offsets) {
                        lockPositionOffsetRef.current = offsets.positionOffset;
                        lockRotationOffsetRef.current = offsets.rotationOffset;
                    }
                }

                if (lockPositionOffsetRef.current && lockRotationOffsetRef.current && firstInputSource.transform) {
                    const controllerMatrix = xrMatrixToMatrix4(firstInputSource.transform.matrix);

                    const newWorldPos = lockPositionOffsetRef.current.clone().applyMatrix4(controllerMatrix);
                    rootRef.current.position.copy(newWorldPos);

                    const controllerQuaternion = xrOrientationToQuaternion(firstInputSource.transform.orientation);

                    const newRotation = controllerQuaternion.clone().multiply(lockRotationOffsetRef.current);
                    rootRef.current.quaternion.copy(newRotation);
                }
            }

            // Scale and Rotate with two controllers/hands
            if (meshRef.current && firstInputSource?.transform && secondInputSource?.transform) {
                const firstPosition = xrPositionToVector3(firstInputSource.transform.position);
                const secondPosition = xrPositionToVector3(secondInputSource.transform.position);

                // Calculate pivot point (midpoint between the two input sources)
                const pivotPoint = firstPosition.clone().add(secondPosition).divideScalar(2);
                const currentDistance = firstPosition.distanceTo(secondPosition);

                // Current connection axis
                const currentConnectionAxis = secondPosition.clone().sub(firstPosition).normalize();

                // Initialize two-hand mode
                if (statesDistanceRef.current === 0) {
                    statesDistanceRef.current = currentDistance;
                    lockScaleRef.current.copy(rootRef.current.scale);
                    lockPivotPointRef.current.copy(pivotPoint);
                    lockModelPositionRef.current.copy(rootRef.current.position);
                    lockModelRotationRef.current.copy(rootRef.current.quaternion);
                    lockFirstOrientationRef.current = xrOrientationToQuaternion(firstInputSource.transform.orientation);
                    lockSecondOrientationRef.current = xrOrientationToQuaternion(secondInputSource.transform.orientation);
                    lockConnectionAxisRef.current.copy(currentConnectionAxis);

                    const worldUp = new Vector3(0, 1, 0);
                    lockUpVectorRef.current.copy(worldUp.clone().sub(currentConnectionAxis.clone().multiplyScalar(worldUp.dot(currentConnectionAxis))).normalize());

                    // Compute local pivot from hit points (scale origin)
                    const firstHit = firstInputSource.hitPoint ?? pivotPoint;
                    const secondHit = secondInputSource.hitPoint ?? pivotPoint;
                    const hitMidpoint = firstHit.clone().add(secondHit).divideScalar(2);
                    const pivotLocal = hitMidpoint.clone().sub(rootRef.current.position);
                    pivotLocal.applyQuaternion(rootRef.current.quaternion.clone().invert());
                    pivotLocal.divide(rootRef.current.scale);
                    lockPivotLocalRef.current.copy(pivotLocal);

                    meshRef.current.position.set(0, 0, 0);
                    return;
                }

                // Calculate scale from distance and rotation
                const distanceScaleFactor = currentDistance / statesDistanceRef.current;
                const combinedScaleFactor = distanceScaleFactor;
                const newScale = lockScaleRef.current.clone().multiplyScalar(combinedScaleFactor);
                newScale.clampScalar(0.1, 20);

                // Translation: rootRef follows hands midpoint movement
                const handsDelta = pivotPoint.clone().sub(lockPivotPointRef.current);
                rootRef.current.position.copy(lockModelPositionRef.current.clone().add(handsDelta));

                // Apply scale
                rootRef.current.scale.copy(newScale);

                // Compensate with meshRef so scaling originates from grab point
                const initialScaleScalar = lockScaleRef.current.x;
                const newScaleScalar = newScale.x;
                meshRef.current.position.copy(lockPivotLocalRef.current.clone().multiplyScalar(initialScaleScalar / newScaleScalar - 1));
            }
        }

        rootRef.current.getWorldPosition(objectPositionRef.current);
        rootRef.current.getWorldQuaternion(objectRotationRef.current);
    });

    return (
        <>
            {leftController && <primitive object={leftControllerArrowHelperRef.current} />}
            {rightController && <primitive object={rightControllerArrowHelperRef.current} />}

            {leftHand && <primitive object={leftHandArrowHelperRef.current} />}
            {rightHand && <primitive object={rightHandArrowHelperRef.current} />}

            <group
                ref={rootRef}>
                <mesh
                    ref={meshRef}>
                    <primitive
                        object={model} />
                </mesh>

                {metadata?.pois?.map((poi: any, index: number) => {
                    return (
                        <Poi
                            key={index}
                            poi={poi} />
                    );
                })}
            </group>
        </>
    );
}