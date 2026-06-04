import { useRef } from "react";

import { useXRInputSourceState, type XRControllerState } from "@react-three/xr";

import type { RaycastResult } from "../utils";
import { ArrowHelper } from "three";

function isTriggerPressed(state: XRControllerState | null | undefined): boolean {
    // Check if trigger (button 0) is fully pressed
    const button = state?.inputSource.gamepad?.buttons[0];
    return button?.value === 1;
}

export default function useControllers() {
    const leftController = useXRInputSourceState("controller", "left");
    const rightController = useXRInputSourceState("controller", "right");

    const leftControllerTransformRef = useRef<XRRigidTransform | null | undefined>(null);
    const rightControllerTransformRef = useRef<XRRigidTransform | null | undefined>(null);

    const leftControllerRaycastResultRef = useRef<RaycastResult | null | undefined>(null);
    const rightControllerRaycastResultRef = useRef<RaycastResult | null | undefined>(null);

    const leftControllerArrowHelperRef = useRef<ArrowHelper>(new ArrowHelper());
    const rightControllerArrowHelperRef = useRef<ArrowHelper>(new ArrowHelper());

    return {
        leftController,
        rightController,
        leftControllerTransformRef,
        rightControllerTransformRef,
        leftControllerRaycastResultRef,
        rightControllerRaycastResultRef,
        leftControllerArrowHelperRef,
        rightControllerArrowHelperRef,
        isTriggerPressed,
    };
}