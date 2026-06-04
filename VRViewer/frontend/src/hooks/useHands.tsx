import { ArrowHelper, Vector3 } from "three";

import { useXRInputSourceState } from "@react-three/xr";

import type { XRHandState } from "@pmndrs/xr";
import { useRef } from "react";
import type { RaycastResult } from "../utils";

function isPinching(state: XRHandState | null | undefined, baseSpace: XRSpace | null | undefined, frame: XRFrame | null | undefined): boolean {
    // Check if pinch gesture is active (index-thumb distance < 3cm)
    if (!state?.inputSource.hand || !baseSpace || !frame || !frame.getJointPose) return false;

    const indexTip = state.inputSource.hand.get("index-finger-tip");
    const thumbTip = state.inputSource.hand.get("thumb-tip");
    if (!indexTip || !thumbTip) return false;

    const indexPose = frame.getJointPose(indexTip, baseSpace);
    const thumbPose = frame.getJointPose(thumbTip, baseSpace);
    if (!indexPose || !thumbPose) return false;

    const indexPos = new Vector3(
        indexPose.transform.position.x,
        indexPose.transform.position.y,
        indexPose.transform.position.z
    );
    const thumbPos = new Vector3(
        thumbPose.transform.position.x,
        thumbPose.transform.position.y,
        thumbPose.transform.position.z
    );

    const distance = indexPos.distanceTo(thumbPos);
    return distance < 0.03;
}

export default function useHands() {
    const leftHand = useXRInputSourceState("hand", "left");
    const rightHand = useXRInputSourceState("hand", "right");

    const leftHandTransformRef = useRef<XRRigidTransform | null | undefined>(null);
    const rightHandTransformRef = useRef<XRRigidTransform | null | undefined>(null);

    const leftHandRaycastResultRef = useRef<RaycastResult>(null);
    const rightHandRaycastResultRef = useRef<RaycastResult>(null);

    const leftHandArrowHelperRef = useRef<ArrowHelper>(new ArrowHelper());
    const rightHandArrowHelperRef = useRef<ArrowHelper>(new ArrowHelper());

    return {
        leftHand,
        rightHand,
        leftHandTransformRef,
        rightHandTransformRef,
        leftHandRaycastResultRef,
        rightHandRaycastResultRef,
        leftHandArrowHelperRef,
        rightHandArrowHelperRef,
        isPinching,
    };
}