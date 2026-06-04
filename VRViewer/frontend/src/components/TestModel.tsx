import { useEffect, useRef } from "react";

import { Group, Object3D, Quaternion, Vector3, type Object3DEventMap } from "three";

import { useXR, type XRControllerState } from "@react-three/xr";
import { useFrame } from "@react-three/fiber";

import type { XRHandState } from "@pmndrs/xr";

import { useControllers, useHands } from "../hooks";
import { type ModelMetadata } from "../hooks/useModel";

import { evaluatePose, performRaycast, updateArrowHelper, xrOrientationToQuaternion, xrPositionToVector3 } from "../utils";

type Props = {
    model: Group<Object3DEventMap>,
    metadata: ModelMetadata | undefined
};


type XRStateTransform = {
    state: XRControllerState | XRHandState | null | undefined;
    transform: XRRigidTransform | null | undefined;
    hitPoint?: Vector3;
};

// const leftControllerArrowHelper = new ArrowHelper();
// const rightControllerArrowHelper = new ArrowHelper();

// const leftHandArrowHelper = new ArrowHelper();
// const rightHandArrowHelper = new ArrowHelper();

// let leftControllerTransform: XRRigidTransform | null | undefined = null;
// let rightControllerTransform: XRRigidTransform | null | undefined = null;

// let leftControllerRaycastResult: RaycastResult | null | undefined = null;
// let rightControllerRaycastResult: RaycastResult | null | undefined = null;

let initialInputSourceTransform: XRRigidTransform | null | undefined = null;

let initialModelPosition: Vector3 | null | undefined = null;
let initialModelRotation: Quaternion | null | undefined = null;

// let activeStatesTransforms: XRStateTransform[] = [];

export default function TestModel({ model, metadata }: Props) {
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

    // const materialRef = useRef<MeshStandardMaterial>(null);

    // const initialModelPositionRef = useRef<Vector3>(null);
    // const initialModelRotationRef = useRef<Quaternion>(null);

    const activeStatesTransformsRef = useRef<XRStateTransform[]>([]);

    // const [hovered, setHover] = useState(false);

    useEffect(() => {
        if (!metadata) return;

        // metadata.asset.pois.forEach(poi => console.log(poi.position));
    }, [metadata]);

    useEffect(() => {
        // objectPositionRef.current = new Vector3();
        // objectRotationRef.current = new Quaternion();

        // lockPositionOffsetRef.current = lockRotationOffsetRef.current = null;

        // activeStatesTransformsRef.current = [];

        if (session) rootRef.current?.position.set(0, 0.5, -5);
        else rootRef.current?.position.set(0, 0, 0);

        rootRef.current?.quaternion.set(0, 0, 0, 1);
        rootRef.current?.scale.set(1, 1, 1);

        // setHover(false);
    }, [session]);

    // useEffect(() => {
    //     model.traverse(obj => {
    //         if (obj instanceof Mesh && obj.material instanceof MeshStandardMaterial) materialRef.current = obj.material;
    //     });
    // }, [model]);

    // useEffect(() => {
    //     if (hovered) materialRef.current?.color.setRGB(0, 1, 0);
    //     else materialRef.current?.color.setRGB(1, 1, 1);
    // }, [hovered]);

    function dragWithOneInputSource(inputSourceTransform: XRRigidTransform | null | undefined) {
        if (!rootRef.current || !initialInputSourceTransform || !inputSourceTransform || !initialModelPosition || !initialModelRotation) return;

        const initialControllerPosition = xrPositionToVector3(initialInputSourceTransform.position);
        const initialControllerRotation = xrOrientationToQuaternion(initialInputSourceTransform.orientation);

        const currentControllerPosition = xrPositionToVector3(inputSourceTransform.position);
        const currentControllerRotation = xrOrientationToQuaternion(inputSourceTransform.orientation);

        // Calculates position offset
        const initialOffset = initialModelPosition.clone().sub(initialControllerPosition);

        // Calculates relative controller rotation
        const deltaQuaternion = new Quaternion();
        deltaQuaternion.multiplyQuaternions(
            currentControllerRotation,
            initialControllerRotation.clone().invert()
        );

        // Rotates position offset with the controller rotation
        const rotatedOffset = initialOffset.clone().applyQuaternion(deltaQuaternion);

        // Calculates new position
        const newPosition = currentControllerPosition.clone().add(rotatedOffset);

        // Calculates new rotation
        const newRotation = new Quaternion();
        newRotation.multiplyQuaternions(deltaQuaternion, initialModelRotation);

        // Apply transforms
        rootRef.current.position.copy(newPosition);
        rootRef.current.quaternion.copy(newRotation);
    }

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

        // If the primary input source was released or there are no active sources, reset the drag state
        if (reset) {
            initialInputSourceTransform = null;
            initialModelPosition = null;
            initialModelRotation = null;
        }

        // If there are no active input sources, nothing to drag
        if (activeStatesTransformsRef.current.length === 0) return;

        // Use the first active input source for dragging
        const primaryInputSource = activeStatesTransformsRef.current[0];

        // Initialize the drag on first contact
        if (!initialInputSourceTransform && primaryInputSource.transform) {
            initialInputSourceTransform = primaryInputSource.transform;

            initialModelPosition = rootRef.current.position.clone();
            initialModelRotation = rootRef.current.quaternion.clone();
        }

        // Perform the drag with the primary input source's current transform
        if (primaryInputSource.transform) dragWithOneInputSource(primaryInputSource.transform);
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

                {/* {(metadata?.asset as any).pois.map((poi: any, index: number) => {
                    return (
                        <Poi
                            key={index}
                            poi={poi} />
                    );
                })} */}
            </group>
        </>
    );
}