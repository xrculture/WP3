const IDLE_MODE = 0;
const ROTATE_MODE = 1;
const ZOOM_MODE = 2;
const PAN_MODE = 3;
const ZOOM_PAN_MODE = 4;

const ZOOM_FACTOR = 0.05;
const ZOOM_FACTOR_MOUSE_WHEEL = 0.01;
const ROTATE_FACTOR = 0.35;
const PAN_FACTOR = 0.0015;

var g_uiMode = IDLE_MODE;
var g_uiMoveInProgress = false;
var g_uiStartX = -1;
var g_uiStartY = -1;
var g_uiX = -1;
var g_uiY = -1;
var g_uiZoomStartX = -1;
var g_uiZoomStartY = -1;
var g_uiTouchesDistance = -1;

function resetUIData() {
    g_uiMode = IDLE_MODE;
    g_uiMoveInProgress = false;
    g_uiStartX = -1;
    g_uiStartY = -1;
    g_uiX = -1;
    g_uiY = -1;
    g_uiZoomStartX = -1;
    g_uiZoomStartY = -1;
    g_uiTouchesDistance = -1;
}

function _getCamera() {
    try {
        return JSON.stringify(g_viewer.getCamera());
    } catch (e) {
        window.logErr(e);
    }
    return JSON.stringify({});
}

function _setCamera(camera) {
    try {
        return g_viewer.setCamera(camera);
    } catch (e) {
        window.logErr(e);
    }
}

function _setRotationXYZ(x, y, z) {
    try {
        g_viewer._rotateQuat = fromEulerXYZ(x, y, z);

        PENDING_DRAW_SCENE = true;
    }
    catch (e) {
        window.logErr(e);
    }
}

function _setRotationXZ(x, z) {
    try {
        g_viewer._rotateQuat = fromEulerXZ(x, z);

        PENDING_DRAW_SCENE = true;
    }
    catch (e) {
        window.logErr(e);
    }
}

function _showFaces(show) {
    try {
        g_viewer._showConceptualFaces = show;

        PENDING_DRAW_SCENE = true;
    }
    catch (e) {
        window.logErr(e);
    }
}

function _showWireframes(show) {
    try {
        g_viewer._showConceptualFacesPolygons = show;

        PENDING_DRAW_SCENE = true;
    }
    catch (e) {
        window.logErr(e);
    }
}

function _showLines(show) {
    try {
        g_viewer._showLines = show;

        PENDING_DRAW_SCENE = true;
    }
    catch (e) {
        window.logErr(e);
    }
}

function _showPoints(show) {
    try {
        g_viewer._showPoints = show;

        PENDING_DRAW_SCENE = true;
    }
    catch (e) {
        window.logErr(e);
    }
}

function _showModelCS(show) {
    try {
        g_viewer._showModelCS = show;

        PENDING_DRAW_SCENE = true;
    }
    catch (e) {
        window.logErr(e);
    }
}

function _showWorldCS(show) {
    try {
        g_viewer._showWorldCS = show;

        PENDING_DRAW_SCENE = true;
    }
    catch (e) {
        window.logErr(e);
    }
}

function _showNavigator(show) {
    try {
        g_viewer._showNavigator = show;

        PENDING_DRAW_SCENE = true;
    }
    catch (e) {
        window.logErr(e);
    }
}

function _zoom(zoomIn, factor, redraw) {
    try {
        // Calculate the vector from eye to target
        const eyeToTarget = vec3.create();
        vec3.subtract(eyeToTarget, g_viewer._targetVector, g_viewer._eyeVector);

        // Get current distance
        const currentDistance = vec3.length(eyeToTarget);

        // Calculate zoom amount as a percentage of current distance
        // This makes zoom speed proportional to distance (feels more natural)
        const zoomAmount = currentDistance * factor * (zoomIn ? 1 : -1);

        // Ensure we don't zoom too close or too far
        const MIN_DISTANCE = 0.15;
        const MAX_DISTANCE = g_viewer._worldDimensions.MaxDistance * 5;

        // Normalize the direction vector
        vec3.normalize(eyeToTarget, eyeToTarget);

        // Scale by zoom amount
        vec3.scale(eyeToTarget, eyeToTarget, zoomAmount);

        // Apply to eye position (move along the view direction)
        vec3.add(g_viewer._eyeVector, g_viewer._eyeVector, eyeToTarget);

        // Check if we're too close or too far after zoom
        const newEyeToTarget = vec3.create();
        vec3.subtract(newEyeToTarget, g_viewer._targetVector, g_viewer._eyeVector);
        const newDistance = vec3.length(newEyeToTarget);

        if (newDistance < MIN_DISTANCE || newDistance > MAX_DISTANCE) {
            // Recalculate eye position to maintain proper distance
            vec3.normalize(newEyeToTarget, newEyeToTarget);

            const clampedDistance = Math.max(MIN_DISTANCE, Math.min(newDistance, MAX_DISTANCE));
            vec3.scale(newEyeToTarget, newEyeToTarget, clampedDistance);
            vec3.subtract(g_viewer._eyeVector, g_viewer._targetVector, newEyeToTarget);
        }

        if (redraw === undefined || redraw === true) {
            PENDING_DRAW_SCENE = true;
        }
    } catch (e) {
        window.logErr(e);
    }
}

function _zoomTo(Xmin, Ymin, Zmin, Xmax, Ymax, Zmax) {
    try {
        g_viewer.zoomTo(Xmin, Ymin, Zmin, Xmax, Ymax, Zmax);
    } catch (e) {
        window.logErr(e);
    }
}

function _zoomToXZ(Xmin, Ymin, Zmin, Xmax, Ymax, Zmax) {
    try {
        g_viewer.zoomToXZ(Xmin, Ymin, Zmin, Xmax, Ymax, Zmax);
    } catch (e) {
        window.logErr(e);
    }
}

function _resetView() {
    try {
        g_viewer.resetView(true);
    } catch (e) {
        window.logErr(e);
    }
}

function _reset() {
    try {
        g_viewer.reset();
    } catch (e) {
        window.logErr(e);
    }
}

function _selectInstancesByID(IDs) {
    try {
        g_viewer.selectInstancesByID(IDs);
    } catch (e) {
        window.logErr(e);
    }
}

function _highlightInstancesByID(IDs) {
    try {
        g_viewer.highlightInstancesByID(IDs);
    } catch (e) {
        window.logErr(e);
    }
}

function _showInstancesByID(IDs, show) {
    try {
        g_viewer.showInstancesByID(IDs, show);
    } catch (e) {
        window.logErr(e);
    }
}

function _getSelectedInstances() {
    try {
        return JSON.stringify(g_viewer._selectedInstances);
    } catch (e) {
        window.logErr(e);
    }
    return JSON.stringify([]);
}

function _getHighlightedInstances() {
    try {
        return JSON.stringify(g_viewer._highlightedInstances);
    } catch (e) {
        window.logErr(e);
    }
    return JSON.stringify([]);
}

function _setSelectedInstanceMaterial(material) {
    try {
        g_viewer.SELECTED_INSTANCE_MATERIAL = material;

        PENDING_DRAW_SCENE = true;
    } catch (e) {
        window.logErr(e);
    }
}

function _setHighlightedInstanceMaterial(material) {
    try {
        g_viewer.HIGHLIGHTED_INSTANCE_MATERIAL = material;

        PENDING_DRAW_SCENE = true;
    } catch (e) {
        window.logErr(e);
    }
}

function zoom(zoomIn) {
    if (!g_uiMoveInProgress) {
        return;
    }
    _zoom(zoomIn, ZOOM_FACTOR);
}

function mouseWheelZoom(zoomIn) {
    _zoom(zoomIn, ZOOM_FACTOR_MOUSE_WHEEL);
}

function rotate(x, y, z = 0) {
    try {
        if (!g_uiMoveInProgress) {
            return;
        }

        let deltaX = (x - g_uiX);
        let deltaY = (y - g_uiY);

        // If movement is more pronounced in one direction, reduce the other to create a more intuitive feel
        if (deltaX !== deltaY) {
            if (deltaX > deltaY) {
                deltaY /= 2;
            } else {
                deltaX /= 2;
            }
        }

        // X/Y/Z
        g_viewer.orbitXYZ(deltaX * ROTATE_FACTOR, deltaY * ROTATE_FACTOR, 0);

        // X/Z
        //g_viewer.orbitXZ(deltaX * ROTATE_FACTOR, -deltaY * ROTATE_FACTOR);

        g_uiX = x;
        g_uiY = y;
    }
    catch (e) {
        window.logErr(e);
    }
}

function pan(x, y) {
    try {
        if (!g_uiMoveInProgress) {
            return;
        }

        // Calculate screen space movement since last frame
        const deltaX = x - g_uiX;
        const deltaY = y - g_uiY;

        if (deltaX === 0 && deltaY === 0) {
            return;
        }

        let forward, right, up;

        // Extract the camera vectors from the current transformation matrix to account for quaternion rotations
        if (g_viewer._rotateAroundWorldCenter) {
            // Get the current model-view matrix to extract camera orientation
            const tempModelView = mat4.create();
            mat4.identity(tempModelView);
            mat4.lookAt(
                tempModelView,
                g_viewer._eyeVector,
                g_viewer._targetVector,
                g_viewer._upVector
            );

            // Apply rotation around the rotation center
            const centerToOrigin = mat4.create();
            mat4.identity(centerToOrigin);
            mat4.translate(
                centerToOrigin,
                centerToOrigin,
                vec3.fromValues(
                    -g_viewer._rotationCenter[0],
                    -g_viewer._rotationCenter[1],
                    -g_viewer._rotationCenter[2]
                )
            );

            const rotationMatrix = mat4.create();
            mat4.fromQuat(rotationMatrix, g_viewer._rotateQuat);

            const originToCenter = mat4.create();
            mat4.identity(originToCenter);
            mat4.translate(
                originToCenter,
                originToCenter,
                vec3.fromValues(
                    g_viewer._rotationCenter[0],
                    g_viewer._rotationCenter[1],
                    g_viewer._rotationCenter[2]
                )
            );

            const transformMatrix = mat4.create();
            mat4.multiply(transformMatrix, originToCenter, rotationMatrix);
            mat4.multiply(transformMatrix, transformMatrix, centerToOrigin);
            mat4.multiply(tempModelView, tempModelView, transformMatrix);

            // Extract camera vectors from the transformation matrix
            const invMatrix = mat4.create();
            mat4.invert(invMatrix, tempModelView);

            // Extract the camera's local axes from the inverse matrix
            right = vec3.fromValues(invMatrix[0], invMatrix[1], invMatrix[2]);
            up = vec3.fromValues(invMatrix[4], invMatrix[5], invMatrix[6]);
            forward = vec3.fromValues(-invMatrix[8], -invMatrix[9], -invMatrix[10]);

            vec3.normalize(right, right);
            vec3.normalize(up, up);
            vec3.normalize(forward, forward);
        } else {
            // Use simple camera vectors
            forward = vec3.create();
            vec3.subtract(forward, g_viewer._targetVector, g_viewer._eyeVector);
            vec3.normalize(forward, forward);

            // Right vector is perpendicular to forward and up
            right = vec3.create();
            vec3.cross(right, forward, g_viewer._upVector);
            vec3.normalize(right, right);

            // Recalculate the true up vector to ensure orthogonality
            up = vec3.create();
            vec3.cross(up, right, forward);
            vec3.normalize(up, up);
        }

        // Scale factors to control pan sensitivity
        // Calculate current distance between camera and target (zoom level)
        const eyeToTarget = vec3.create();
        vec3.subtract(eyeToTarget, g_viewer._targetVector, g_viewer._eyeVector);
        const currentDistance = vec3.length(eyeToTarget);

        // Make pan sensitivity proportional to current zoom level
        const panSensitivity = currentDistance * PAN_FACTOR;
        const rightAmount = -deltaX * panSensitivity;
        const upAmount = deltaY * panSensitivity;

        // Calculate the combined movement vector
        const moveVec = vec3.create();
        const rightVec = vec3.create();
        const upVec = vec3.create();

        vec3.scale(rightVec, right, rightAmount);
        vec3.scale(upVec, up, upAmount);
        vec3.add(moveVec, rightVec, upVec);

        // Move both eye and target by the same amount to maintain view direction
        vec3.add(g_viewer._eyeVector, g_viewer._eyeVector, moveVec);
        vec3.add(g_viewer._targetVector, g_viewer._targetVector, moveVec);

        // Update UI state
        g_uiX = x;
        g_uiY = y;

        PENDING_DRAW_SCENE = true;
    }
    catch (e) {
        window.logErr(e);
    }
}

window.addEventListener(
    'load',
    function () {
        const TOUCH_ACTION_MIN_DISTANCE = 2.5;

        const canvas = document.getElementById('canvas-element-id');
        const canvasRect = canvas.getBoundingClientRect();

        //
        // Touch support
        //

        // Prevent default touch actions to avoid scrolling and other browser behaviors
        function touchEventPreventDefault(event) {
            event.preventDefault();
            event.stopPropagation();
        }

        // Check if touch points are within canvas boundaries
        function touchesInCanvasBounds(event) {
            for (let i = 0; i < event.touches.length; i++) {
                const touch = event.touches[i];
                if (touch.clientX < canvasRect.left ||
                    touch.clientX > canvasRect.right ||
                    touch.clientY < canvasRect.top ||
                    touch.clientY > canvasRect.bottom) {
                    return false;
                }
            }
            return true;
        }

        canvas.addEventListener(
            'touchstart',
            function (event) {
                if (!touchesInCanvasBounds(event)) {
                    return; // Ignore touches outside canvas
                }

                touchEventPreventDefault(event);

                try {
                    resetUIData();

                    if (event.touches.length == 2) {
                        // Calculate initial midpoint between the two touches
                        const midpointX = (event.touches[0].pageX + event.touches[1].pageX) / 2;
                        const midpointY = (event.touches[0].pageY + event.touches[1].pageY) / 2;

                        // Initialize both starting and current positions
                        g_uiStartX = g_uiX = midpointX;
                        g_uiStartY = g_uiY = midpointY;

                        // Calculate touch distance for potential zoom detection
                        g_uiTouchesDistance = Math.sqrt(
                            Math.pow(event.touches[1].pageX - event.touches[0].pageX, 2) +
                            Math.pow(event.touches[1].pageY - event.touches[0].pageY, 2));

                        g_uiMode = ZOOM_PAN_MODE; // Default to Zoom/Pan mode
                    } else if (event.touches.length == 1) {
                        g_uiStartX = g_uiX = event.touches[0].pageX;
                        g_uiStartY = g_uiY = event.touches[0].pageY;
                        g_uiMode = ROTATE_MODE; // Default to Rotate mode
                    }

                    g_uiMoveInProgress = true;
                } catch (e) {
                    window.logErr(e);
                }
            }
        );

        canvas.addEventListener(
            'touchmove',
            function (event) {
                if (!touchesInCanvasBounds(event)) {
                    return; // Ignore touches outside canvas
                }

                touchEventPreventDefault(event);

                try {
                    g_uiMoveInProgress = true;

                    if (event.touches.length == 2) {
                        if (g_uiMode !== ZOOM_PAN_MODE) {
                            return;
                        }

                        // Calculate distance between the two touches
                        var touchesDistance = Math.sqrt(
                            Math.pow(event.touches[1].pageX - event.touches[0].pageX, 2) +
                            Math.pow(event.touches[1].pageY - event.touches[0].pageY, 2));

                        // Calculate midpoint between the two touches
                        const midpointX = (event.touches[0].pageX + event.touches[1].pageX) / 2;
                        const midpointY = (event.touches[0].pageY + event.touches[1].pageY) / 2;
                        const moveDistance = Math.sqrt(
                            Math.pow(midpointX - g_uiX, 2) +
                            Math.pow(midpointY - g_uiY, 2));

                        // Zoom
                        const distanceChangeRatio = Math.abs(touchesDistance - g_uiTouchesDistance) / g_uiTouchesDistance;
                        if (Math.abs(touchesDistance - g_uiTouchesDistance) > Math.abs(moveDistance)) {
                            _zoom(touchesDistance > g_uiTouchesDistance ? true : false,
                                distanceChangeRatio,
                                false);                            
                        }

                        // Pan
                        pan(midpointX, midpointY);

                        g_uiTouchesDistance = touchesDistance;
                        g_uiX = midpointX;
                        g_uiY = midpointY;
                    }
                    else {
                        // Rotate
                        if ((event.touches.length == 1) && (g_uiMode === ROTATE_MODE)) {
                            rotate(event.touches[0].pageX, event.touches[0].pageY);

                            g_uiX = event.touches[0].pageX;
                            g_uiY = event.touches[0].pageY;
                        }
                    }
                } catch (e) {
                    window.logErr(e);
                }
            }
        );

        canvas.addEventListener(
            'touchend',
            function () {
                if ((g_uiMode === ROTATE_MODE) &&
                    (Math.abs(g_uiX - g_uiStartX) < TOUCH_ACTION_MIN_DISTANCE) &&
                    (Math.abs(g_uiY - g_uiStartY) < TOUCH_ACTION_MIN_DISTANCE)) {
                    g_viewer.selectInstanceAt(g_uiX, g_uiY);
                }

                resetUIData();
            }
        );

        canvas.addEventListener(
            'touchcancel',
            function () {
                resetUIData();
            }
        );

        //
        // Mouse support
        //

        canvas.addEventListener(
            'mousedown',
            function (event) {
                try {
                    resetUIData();

                    switch (event.button) {
                        case 0: {
                            g_uiMode = ROTATE_MODE;
                        }
                            break;

                        case 1: {
                            g_uiMode = ZOOM_MODE;
                        }
                            break;

                        case 2: {
                            g_uiMode = PAN_MODE;
                        }
                            break;
                    }

                    g_uiStartX = g_uiX = event.clientX;
                    g_uiStartY = g_uiY = event.clientY;
                } catch (e) {
                    window.logErr(e);
                }
            }
        );

        canvas.addEventListener(
            'mousemove',
            function (event) {
                try {
                    g_uiMoveInProgress = true;

                    switch (g_uiMode) {
                        case ROTATE_MODE: {
                            rotate(event.clientX, event.clientY);
                        }
                            break;

                        case ZOOM_MODE: {
                            if (g_uiZoomStartX != -1 && g_uiZoomStartY != -1) {
                                if (Math.abs(event.clientX - g_uiX) != Math.abs(event.clientY - g_uiY)) {
                                    let zoomIn = true;
                                    if (Math.abs(event.clientX - g_uiX) > Math.abs(event.clientY - g_uiY)) {
                                        zoomIn = event.clientX >= g_uiX ? true : false;
                                    } else {
                                        zoomIn = event.clientY >= g_uiY ? false : true;
                                    }

                                    zoom(zoomIn);
                                }
                            }
                            else {
                                g_uiZoomStartX = event.clientX;
                                g_uiZoomStartY = event.clientY;
                            }
                        }
                            break;

                        case PAN_MODE: {
                            pan(event.clientX, event.clientY);
                        }
                            break;
                    } // switch (g_uiMode)

                    g_uiX = event.clientX;
                    g_uiY = event.clientY;
                }
                catch (e) {
                    window.logErr(e);
                }
            }
        );

        canvas.addEventListener(
            'mousewheel',
            function (event) {
                if (navigator.userAgent.toLowerCase().indexOf('firefox') > -1) {
                    mouseWheelZoom(event.detail < 0);
                } else {
                    mouseWheelZoom(event.wheelDelta > 0);
                }
            }
        );

        canvas.addEventListener(
            'mouseup',
            function (event) {
                try {
                    if ((g_uiMode === ROTATE_MODE) && !g_uiMoveInProgress) {
                        if (event.altKey) {
                            const instanceId = g_viewer.getInstanceAt(event.clientX, event.clientY);
                            if (instanceId && (instanceId > 0)) {
                                const instanceBB = Module.getInstanceBB(instanceId);
                                if (instanceBB && (instanceBB.size() === 6)) {
                                    g_viewer.zoomToXZ(
                                        instanceBB.get(0), instanceBB.get(1), instanceBB.get(2),
                                        instanceBB.get(3), instanceBB.get(4), instanceBB.get(5));
                                }
                            }
                        }
                        else {
                            if (event.ctrlKey) {
                                MULTI_SELECTION_MODE = true;
                            }
                            g_viewer.selectInstanceAt(event.clientX, event.clientY);
                            MULTI_SELECTION_MODE = false;
                        }                        
                    }

                    resetUIData();
                }
                catch (e) {
                    window.logErr(e);
                }
            }
        );

        canvas.addEventListener(
            'mouseleave',
            function () {
                resetUIData();
            }
        );
    }
);


