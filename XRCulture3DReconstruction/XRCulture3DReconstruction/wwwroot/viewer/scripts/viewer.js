/**
 * Global variables
 */
var MULTI_SELECTION_MODE = false;
var SELECTED_INSTANCE_MATERIAL = {
    ambient: [0.9, 0.0, 0.0],
    specular: [0.5, 0.0, 0.0],
    diffuse: [0.5, 0.0, 0.0],
    emissive: [0.5, 0.0, 0.0],
    transparency: 1.0,
    texture: null,
};
var HIGHLIGHTED_INSTANCE_MATERIAL = {
    ambient: [0.0, 0.0, 0.9],
    specular: [0.0, 0.0, 0.5],
    diffuse: [0.0, 0.0, 0.5],
    emissive: [0.0, 0.0, 0.5],
    transparency: 1.0,
};

/**
 * Custom event handlers
 */
var g_onWebGLInitializedEvent = null;
var g_onSelectEvent = null;

/**
 * Euler => Quaternion
 */
function fromEulerXYZ(x, y, z) {
    // Convert angles from degrees to radians
    const radX = (x * Math.PI) / 180;
    const radY = (y * Math.PI) / 180;
    const radZ = (z * Math.PI) / 180;

    // Create quaternions for each axis rotation
    const qX = quat.create();
    const qY = quat.create();
    const qZ = quat.create();

    // Set axis rotations
    quat.setAxisAngle(qX, [1, 0, 0], radX);
    quat.setAxisAngle(qY, [0, 1, 0], radY);
    quat.setAxisAngle(qZ, [0, 0, 1], radZ);

    // Combine rotations (order matters: Z * Y * X)
    const q = quat.create();
    quat.multiply(q, qY, qX);
    quat.multiply(q, qZ, q);

    // Ensure the quaternion is normalized
    quat.normalize(q, q);

    return q;
}

function fromEulerXZ(x, z) {
    //  Create quaternions for X/Z rotation
    const qX = quat.create();
    const qZ = quat.create();

    // Set axis rotations
    quat.setAxisAngle(qX, [1, 0, 0], (x * Math.PI) / 180);
    quat.setAxisAngle(qZ, [0, 0, 1], (z * Math.PI) / 180);

    // Combine initial rotations
    const q = quat.create();
    quat.multiply(q, qX, qZ);

    // Ensure the quaternion is normalized
    quat.normalize(q, q);

    return q;
}

function fromEulerXYZ(x, y, z) {
    const qX = quat.create();
    const qY = quat.create();
    const qZ = quat.create();

    quat.setAxisAngle(qX, [1, 0, 0], (x * Math.PI) / 180);
    quat.setAxisAngle(qY, [0, 1, 0], (y * Math.PI) / 180);
    quat.setAxisAngle(qZ, [0, 0, 1], (z * Math.PI) / 180);

    const q = quat.create();
    quat.multiply(q, qY, qX);  // q = qY * qX
    quat.multiply(q, qZ, q);   // q = qZ * qY * qX

    quat.normalize(q, q);

    return q;
}

/**
 * Quaternion => Euler
 */
function toEulerXYZ(q) {
    const w = q[3];
    const x = q[0];
    const y = q[1];
    const z = q[2];

    const wx = w * x,
        wy = w * y,
        wz = w * z;
    const xx = x * x,
        xy = x * y,
        xz = x * z;
    const yy = y * y,
        yz = y * z,
        zz = z * z;

    const xyz = [
        -Math.atan2(2 * (yz - wx), 1 - 2 * (xx + yy)),
        Math.asin(2 * (xz + wy)),
        -Math.atan2(2 * (xy - wz), 1 - 2 * (yy + zz)),
    ];

    return xyz.map((x) => (x * 180) / Math.PI);
}

/**
 * Viewer
 */
var Viewer = function () {
    /**************************************************************************
     * Members
     */

    /**
     * OpenGL
     */
    this._shaderProgram = null;

    /**
     * Scene
     */
    this._loadingModel = false;

    /**
     * Model
     */
    this._model = {};
    this._geometries = [];
    this._id2instance = {};
    this._worldDimensions = { Xmin: -1.0, Ymin: -1.0, Zmin: -1.0, Xmax: 1.0, Ymax: 1.0, Zmax: 1.0, MaxDistance: 2.0 };
    this._scaleFactor = 1.0;

    /**
     * Decoration Models
     */
    this._decorationModels = [];

    /**
     * Scene
     */
    this._mtxModelView = mat4.create();
    this._mtxProjection = mat4.create();
    this._mtxInversePMV = mat4.create();
    this._clearColor = [0.9, 0.9, 0.9, 1.0];
    this._pointLightPosition = vec3.fromValues(0.25, 0.25, 1);
    this._materialShininess = 50.0;
    this._defaultEyeVector = [0, 0, -5];
    this._eyeVector = vec3.fromValues(this._defaultEyeVector[0], this._defaultEyeVector[1], this._defaultEyeVector[2]);
    this._targetVector = vec3.fromValues(0, 0, 0);
    this._upVector = vec3.fromValues(0, 1, 0);
    //this._rotateQuat = fromEulerXZ(-135, 225); // X/Y
    this._rotateQuat = fromEulerXYZ(225, 45, 315); // X/Y/Z
    this._rotationCenter = vec3.fromValues(0, 0, 0);
    this._rotateAroundWorldCenter = false; // false => rotate around Model center/ZoomTo center

    /**
     * Visibility
     */
    this._showConceptualFaces = true;
    this._showConceptualFacesPolygons = false;
    this._showLines = true;
    this._showPoints = true;

    /**
     * Decorations
     */
    this._showModelCS = true;
    this._showWorldCS = false;
    this._showNavigator = true;
    this.NAVIGATION_VIEW_SIZE = 250;

    /**
     * Selection
     */
    this._selectedInstances = [];
    this._highlightedInstances = [];
    this._selectionBuffer = null;
    this.SELECTION_BUFFER_SIZE = 512;

    /**
     * Textures
     */
    this._defaultTexture = null;
    this._textures = {};
    this._canvas = null;

    /**************************************************************************
     * General
     */

    /**
     * Initialize
     */
    Viewer.prototype.initProgram = function () {
        var fgShader = utils.getShader(gl, 'shader-fs');
        var vxShader = utils.getShader(gl, 'shader-vs');

        this._shaderProgram = gl.createProgram();
        gl.attachShader(this._shaderProgram, vxShader);
        gl.attachShader(this._shaderProgram, fgShader);
        gl.linkProgram(this._shaderProgram);

        if (!gl.getProgramParameter(this._shaderProgram, gl.LINK_STATUS)) {
            window.logErr('[Viewer.initProgram] Could not initialize shaders.');
            return false;
        }

        gl.useProgram(this._shaderProgram);

        /* Vertex Shader */
        this._shaderProgram.VertexPosition = gl.getAttribLocation(
            this._shaderProgram,
            'Position'
        );
        this._shaderProgram.VertexNormal = gl.getAttribLocation(
            this._shaderProgram,
            'Normal'
        );
        this._shaderProgram.UV = gl.getAttribLocation(
            this._shaderProgram,
            'UV'
        );
        this._shaderProgram.ProjectionMatrix = gl.getUniformLocation(
            this._shaderProgram,
            'ProjectionMatrix'
        );
        this._shaderProgram.ModelViewMatrix = gl.getUniformLocation(
            this._shaderProgram,
            'ModelViewMatrix'
        );
        this._shaderProgram.NormalMatrix = gl.getUniformLocation(
            this._shaderProgram,
            'NormalMatrix'
        );
        this._shaderProgram.DiffuseMaterial = gl.getUniformLocation(
            this._shaderProgram,
            'DiffuseMaterial'
        );
        this._shaderProgram.EnableLighting = gl.getUniformLocation(
            this._shaderProgram,
            'EnableLighting'
        );
        this._shaderProgram.EnableTexture = gl.getUniformLocation(
            this._shaderProgram,
            'EnableTexture'
        );

        /* Fragment Shader */
        this._shaderProgram.LightPosition = gl.getUniformLocation(
            this._shaderProgram,
            'LightPosition'
        );
        this._shaderProgram.AmbientMaterial = gl.getUniformLocation(
            this._shaderProgram,
            'AmbientMaterial'
        );
        this._shaderProgram.SpecularMaterial = gl.getUniformLocation(
            this._shaderProgram,
            'SpecularMaterial'
        );
        this._shaderProgram.Transparency = gl.getUniformLocation(
            this._shaderProgram,
            'Transparency'
        );
        // #todo
        //this._shaderProgram.uMaterialEmissiveColor = gl.getUniformLocation(
        //  this._shaderProgram,
        //  'uMaterialEmissiveColor'
        //)
        this._shaderProgram.Shininess = gl.getUniformLocation(
            this._shaderProgram,
            'Shininess'
        );
        this._shaderProgram.AmbientLightWeighting = gl.getUniformLocation(
            this._shaderProgram,
            'AmbientLightWeighting'
        );
        this._shaderProgram.DiffuseLightWeighting = gl.getUniformLocation(
            this._shaderProgram,
            'DiffuseLightWeighting'
        );
        this._shaderProgram.SpecularLightWeighting = gl.getUniformLocation(
            this._shaderProgram,
            'SpecularLightWeighting'
        );
        this._shaderProgram.Sampler = gl.getUniformLocation(
            this._shaderProgram,
            'Sampler'
        );
        this._defaultTexture = createTexture_WASM_FS.call(this, '/data/texture.jpg', false);

        return true;
    }

    /**
     * Lights
     */
    Viewer.prototype.setLights = function () {
        gl.uniform3f(
            this._shaderProgram.LightPosition,
            this._pointLightPosition[0],
            this._pointLightPosition[1],
            this._pointLightPosition[2]
        );
        gl.uniform1f(
            this._shaderProgram.Shininess,
            this._materialShininess
        );
        gl.uniform3f(
            this._shaderProgram.AmbientLightWeighting,
            0.4, 0.4, 0.4
        );
        gl.uniform3f(
            this._shaderProgram.DiffuseLightWeighting,
            0.95, 0.95, 0.95
        );
        gl.uniform3f(
            this._shaderProgram.SpecularLightWeighting,
            0.15, 0.15, 0.15
        );
    }

    /**
     * Initialize
     */
    Viewer.prototype.init = function (canvasID, width, height) {
        gl = utils.getGLContext(canvasID);
        if (!gl) {
            window.logErr('[Viewer.init] Could not initialize WebGL.')
            return false;
        }

        if (!this.initProgram()) {
            return false;
        }

        // Fix for WARNING: there is no texture bound to the unit 0
        function createTexture(type, target, count) {
            var data = new Uint8Array(4); // 4 is required to match default unpack alignment of 4.
            var texture = gl.createTexture();

            gl.bindTexture(type, texture);
            gl.texParameteri(type, gl.TEXTURE_MIN_FILTER, gl.NEAREST);
            gl.texParameteri(type, gl.TEXTURE_MAG_FILTER, gl.NEAREST);

            for (let i = 0; i < count; i++) {
                gl.texImage2D(
                    target + i,
                    0,
                    gl.RGBA,
                    1,
                    1,
                    0,
                    gl.RGBA,
                    gl.UNSIGNED_BYTE,
                    data
                );
            }

            return texture;
        }

        var emptyTextures = {};
        emptyTextures[gl.TEXTURE_2D] = createTexture(
            gl.TEXTURE_2D,
            gl.TEXTURE_2D,
            1);
        emptyTextures[gl.TEXTURE_CUBE_MAP] = createTexture(
            gl.TEXTURE_CUBE_MAP,
            gl.TEXTURE_CUBE_MAP_POSITIVE_X,
            6)

        gl.activeTexture(gl.TEXTURE0);
        gl.bindTexture(gl.TEXTURE_2D, emptyTextures[gl.TEXTURE_2D]);

        gl.activeTexture(gl.TEXTURE1);
        gl.bindTexture(gl.TEXTURE_CUBE_MAP, emptyTextures[gl.TEXTURE_CUBE_MAP]);
        // END WARNING: there is no texture bound to the unit 0

        resizeCanvas(width, height);

        this._selectionBuffer = new SelectionBuffer(gl);
        this._selectionBuffer.initialize(this.SELECTION_BUFFER_SIZE, this.SELECTION_BUFFER_SIZE);
        this.loadModel([]);

        this.setLights();
        renderLoop();

        return true;
    }

    /**
     * Projection matrix
     */
    Viewer.prototype.setProjectionMatrix = function (width, height) {
        mat4.identity(this._mtxProjection);

        const fovY = 45.0; // Field of view in degrees
        const aspect = width / height;
        const zNear = 0.001;
        const zFar = 100.0;

        // Calculate frustum dimensions at near plane
        const fH = Math.tan((fovY * Math.PI) / 360.0) * zNear;
        const fW = fH * aspect;

        mat4.frustum(
            this._mtxProjection,
            -fW,    // left
            fW,     // right
            -fH,    // bottom
            fH,     // top
            zNear,  // near
            zFar    // far
        );

        gl.uniformMatrix4fv(
            this._shaderProgram.ProjectionMatrix,
            false,
            this._mtxProjection
        );
    }

    /**
     * Model-View matrix
     */
    Viewer.prototype.setModelViewMatrix = function (applyTranslation = true) {
        mat4.identity(this._mtxModelView);

        if (applyTranslation) {
            // Translation and rotation - Main model, Decoration models (Model Coordinate System)
            mat4.lookAt(
                this._mtxModelView,
                this._eyeVector,
                this._targetVector,
                this._upVector
            );

            // Determine rotation center based on mode
            const rotationCenter = this._rotateAroundWorldCenter
                ? vec3.fromValues(this._targetVector[0], this._targetVector[1], this._targetVector[2]) // World origin
                : this._rotationCenter; // Model center/ZoomTo center

            // Create a matrix to move the rotation center to origin
            const centerToOrigin = mat4.create();
            mat4.identity(centerToOrigin);
            mat4.translate(
                centerToOrigin,
                centerToOrigin,
                vec3.fromValues(
                    -rotationCenter[0],
                    -rotationCenter[1],
                    -rotationCenter[2]
                )
            );

            // Create quaternion rotation matrix
            const rotationMatrix = mat4.create();
            mat4.fromQuat(rotationMatrix, this._rotateQuat);

            // Create matrix to move back from origin to rotation center
            const originToCenter = mat4.create();
            mat4.identity(originToCenter);
            mat4.translate(
                originToCenter,
                originToCenter,
                vec3.fromValues(
                    rotationCenter[0],
                    rotationCenter[1],
                    rotationCenter[2]
                )
            );

            // Apply the transformations: move to origin, rotate, move back
            const transformMatrix = mat4.create();
            mat4.multiply(transformMatrix, originToCenter, rotationMatrix);
            mat4.multiply(transformMatrix, transformMatrix, centerToOrigin);

            // Apply the transformation
            mat4.multiply(this._mtxModelView, this._mtxModelView, transformMatrix);
        }
        else {
            // Rotation only - Decoration models (Navigator/World Coordinate System)
            const fixedEyeVector = vec3.fromValues(0, 0, -5);
            const fixedTargetVector = vec3.fromValues(0, 0, 0);

            mat4.lookAt(
                this._mtxModelView,
                fixedEyeVector,
                fixedTargetVector,
                this._upVector
            );

            const rotationMatrix = mat4.create();
            mat4.fromQuat(rotationMatrix, this._rotateQuat);

            mat4.multiply(this._mtxModelView, this._mtxModelView, rotationMatrix);
        }

        // Calculate inverse Projection-Model-View matrix for selection
        let mtxPMV = mat4.create();
        mat4.multiply(mtxPMV, this._mtxProjection, this._mtxModelView);
        mat4.invert(this._mtxInversePMV, mtxPMV);

        gl.uniformMatrix4fv(
            this._shaderProgram.ModelViewMatrix,
            false,
            this._mtxModelView
        );

        // Normal matrix
        var normalMatrix = mat3.create();
        mat3.fromMat4(normalMatrix, this._mtxModelView);
        gl.uniformMatrix3fv(this._shaderProgram.NormalMatrix, false, normalMatrix);
    }

    /**
     * X/Y/Z axis rotation
     */
    Viewer.prototype.orbitXYZ = function (deltaX, deltaY, deltaZ) {
        try {
            // Convert rotation angles from degrees to radians
            let radDeltaX = (deltaX * Math.PI) / 180;
            let radDeltaY = (deltaY * Math.PI) / 180;
            let radDeltaZ = (deltaZ * Math.PI) / 180;

            // Calculate view direction and other camera vectors
            const viewDir = vec3.create();
            vec3.subtract(viewDir, this._targetVector, this._eyeVector);
            vec3.normalize(viewDir, viewDir);

            // Calculate camera's local coordinate system
            const upVector = vec3.clone(this._upVector);
            vec3.normalize(upVector, upVector);

            // Calculate right vector (perpendicular to view and up)
            const rightVector = vec3.create();
            vec3.cross(rightVector, viewDir, upVector);
            vec3.normalize(rightVector, rightVector);

            // Recalculate true up vector to ensure orthogonality
            const trueUpVector = vec3.create();
            vec3.cross(trueUpVector, rightVector, viewDir);
            vec3.normalize(trueUpVector, trueUpVector);

            // Create rotation quaternions around camera-local axes
            let qRight = quat.create();  // Rotate around right vector (for up/down mouse movement)
            let qUp = quat.create();     // Rotate around up vector (for left/right mouse movement)
            let qView = quat.create();   // Rotate around view vector (for roll)

            quat.setAxisAngle(qRight, rightVector, radDeltaY);
            quat.setAxisAngle(qUp, trueUpVector, radDeltaX);
            quat.setAxisAngle(qView, viewDir, radDeltaZ);

            // Combine the rotations - order matters
            let deltaQuat = quat.create();
            quat.multiply(deltaQuat, qUp, qRight);    // First pitch, then yaw
            quat.multiply(deltaQuat, deltaQuat, qView); // Then roll

            // Apply this delta rotation to our accumulated rotation quaternion
            quat.multiply(this._rotateQuat, deltaQuat, this._rotateQuat);

            // Make sure the quaternion stays normalized
            quat.normalize(this._rotateQuat, this._rotateQuat);

            PENDING_DRAW_SCENE = true;
        } catch (e) {
            window.logErr(e);
        }
    }

    /**
     * X/Z axis rotation
     * https://viewer.sortdesk.com/?sample=rme_advanced_sample_project.ifc
     */
    Viewer.prototype.orbitXZ = function (deltaX, deltaY) {
        try {
            // Convert rotation angles from degrees to radians
            let radDeltaX = (deltaX * Math.PI) / 180;
            let radDeltaY = (deltaY * Math.PI) / 180;

            // Limit vertical rotation to avoid flipping
            const euler = toEulerXYZ(this._rotateQuat);
            if ((euler[0] + deltaY) > -1 || (euler[0] + deltaY) < -179) {
                radDeltaY = 0;
            }

            // For vertical rotation, always use the world X axis
            const worldXAxis = vec3.fromValues(1, 0, 0);

            // For horizontal rotation, transform the world Z axis by the current model rotation
            const modelZAxis = vec3.fromValues(0, 0, 1);
            vec3.transformQuat(modelZAxis, modelZAxis, this._rotateQuat);
            vec3.normalize(modelZAxis, modelZAxis);

            // Create rotation quaternions
            let qVertical = quat.create();   // Rotation around world X axis
            let qModelZ = quat.create();     // Rotation around model Z axis

            // Set up rotations
            quat.setAxisAngle(qVertical, worldXAxis, radDeltaY);
            quat.setAxisAngle(qModelZ, modelZAxis, radDeltaX);

            // Apply horizontal rotation first, then vertical
            let frameQuat = quat.create();
            quat.multiply(frameQuat, qVertical, qModelZ);

            // Apply to accumulated rotation
            quat.multiply(this._rotateQuat, frameQuat, this._rotateQuat);

            // Normalize to prevent drift
            quat.normalize(this._rotateQuat, this._rotateQuat);

            PENDING_DRAW_SCENE = true;
        } catch (e) {
            window.logErr(e);
        }
    }

    /**
     * Extracts camera parameters (eye, target, up) from a Model-View matrix
     */
    Viewer.prototype.extractCameraFromModelViewMatrix = function (modelViewMatrix) {
        // Create an inverse of the model-view matrix
        const invModelView = mat4.create();
        mat4.invert(invModelView, modelViewMatrix);

        // Extract eye position (camera position in world space)
        const eye = vec3.create();
        eye[0] = invModelView[12];
        eye[1] = invModelView[13];
        eye[2] = invModelView[14];

        // Extract camera axes from the rotation part of the matrix
        // The rows of the inverse rotation matrix are the camera's local axes
        const rightAxis = vec3.fromValues(invModelView[0], invModelView[1], invModelView[2]);
        const upAxis = vec3.fromValues(invModelView[4], invModelView[5], invModelView[6]);
        const negLookAxis = vec3.fromValues(invModelView[8], invModelView[9], invModelView[10]);

        // The forward direction is the negative z-axis of the camera space
        const forward = vec3.create();
        vec3.negate(forward, negLookAxis);
        vec3.normalize(forward, forward);

        // Calculate target point at some distance along the look direction
        const target = vec3.create();
        vec3.scaleAndAdd(target, eye, forward, 1.0); // 1.0 is arbitrary distance

        // Up vector is already extracted
        const up = vec3.clone(upAxis);
        vec3.normalize(up, up);

        return {
            eye: eye,
            target: target,
            up: up
        };
    }

    /**
     * Camera
     */
    Viewer.prototype.getCamera = function () {
        const camera = this.extractCameraFromModelViewMatrix(this._mtxModelView);

        // Normalize the up vector, which is a direction
        vec3.normalize(camera.up, camera.up);

        // Calculate distance from eye to target for reference
        const distance = vec3.distance(camera.eye, camera.target);

        // Convert vec3 objects to plain arrays
        return {
            eye: Array.from(camera.eye),
            target: Array.from(camera.target),
            up: Array.from(camera.up),
            distance: distance
        };
    }

    /**
     * Camera
     */
    Viewer.prototype.setCamera = function (camera) {
        if (!camera || !camera.eye || !camera.target || !camera.up) {
            window.logErr("[Viewer.setCamera] Invalid camera object.");
            return;
        }

        this.resetView(false);
        this._rotateQuat = quat.create();

        // Set the camera parameters
        this._eyeVector = vec3.fromValues(camera.eye[0], camera.eye[1], camera.eye[2]);
        this._targetVector = vec3.fromValues(camera.target[0], camera.target[1], camera.target[2]);
        this._upVector = vec3.fromValues(camera.up[0], camera.up[1], camera.up[2]);

        PENDING_DRAW_SCENE = true;
    }

    /**
     * Draws the scene
     */
    Viewer.prototype.drawScene = function () {
        if (this._loadingModel) {
            return;
        }

        this.setProjectionMatrix(gl.canvas.width, gl.canvas.height);
        this.setModelViewMatrix();

        gl.viewport(0, 0, gl.canvas.width, gl.canvas.height);
        gl.clearColor(
            this._clearColor[0],
            this._clearColor[1],
            this._clearColor[2],
            this._clearColor[3]);
        gl.clear(gl.COLOR_BUFFER_BIT | gl.DEPTH_BUFFER_BIT);

        gl.enable(gl.SAMPLE_COVERAGE);
        gl.sampleCoverage(1.0, false);

        gl.enable(gl.DEPTH_TEST);
        gl.depthFunc(gl.LEQUAL);

        this.setLights();
        this.drawInstances();

        // Main model
        this.setProjectionMatrix(gl.canvas.width, gl.canvas.height);
        gl.viewport(0, 0, gl.canvas.width, gl.canvas.height);
        this.setModelViewMatrix();
        this.drawInstancesSelectionBuffer(this._geometries, this._selectionBuffer);

        // Decoration models
        for (let m = 0; m < this._decorationModels.length; m++) {
            const model = this._decorationModels[m];
            if (model.selectionBuffer && model.name === '_NAVIGATOR_') {
                this.setProjectionMatrix(this.NAVIGATION_VIEW_SIZE, this.NAVIGATION_VIEW_SIZE);
                gl.viewport(gl.canvas.width - this.NAVIGATION_VIEW_SIZE, 0, this.NAVIGATION_VIEW_SIZE, this.NAVIGATION_VIEW_SIZE);
                this.setModelViewMatrix(false);
                this.drawInstancesSelectionBuffer(model.geometries, model.selectionBuffer);
            }
        }
    }

    /*
     * Interaction support
     */
    Viewer.prototype.getInstanceAt = function (x, y) {
        if (this._geometries.length === 0 || !this._selectionBuffer) {
            return -1;
        }

        return this._selectionBuffer.getInstanceAt(
            x * (this._selectionBuffer.width / gl.canvas.width),
            (gl.canvas.height - y) * (this._selectionBuffer.height / gl.canvas.height)
        );
    }

    /**
     * Interaction support
     */
    Viewer.prototype.selectInstanceAt = function (x, y)  {
        const instanceId = this.getInstanceAt(x, y);
        if (instanceId === -1) {
            if (this._selectedInstances.length > 0) {
                this._selectedInstances = [];
                PENDING_DRAW_SCENE = true;
            }
            this.selectDecorationInstanceAt(x, y);
            return;
        }

        const selectedInstances = [];
        const instance = this._id2instance[instanceId];
        if (this._id2instance[instance.parentId]) {
            const parentInstance = this._id2instance[instance.parentId];
            for (let i = 0; i < parentInstance.childrenIds.length; i++) {
                const childInstance = this._id2instance[parentInstance.childrenIds[i]];
                if (childInstance) {
                    selectedInstances.push(childInstance.id);
                }
            }
        }
        else {
            selectedInstances.push(instanceId);
        }

        if (MULTI_SELECTION_MODE) {
            for (let i = 0; i < selectedInstances.length; i++) {
                var index = this._selectedInstances.indexOf(selectedInstances[i]);
                if (index === -1) {
                    // Add the instance if it doesn't exist
                    this._selectedInstances.push(selectedInstances[i]);
                }
                else {
                    // Remove it
                    this._selectedInstances.splice(index, 1);
                }
            }
        } // if (MULTI_SELECTION_MODE)
        else {
            this._selectedInstances = [...selectedInstances];
        }

        PENDING_DRAW_SCENE = true;
    }

    /**
     * Interaction support
     */
    Viewer.prototype.selectDecorationInstanceAt = function (x, y) {
        for (let m = 0; m < this._decorationModels.length; m++) {
            const model = this._decorationModels[m];
            if (model.selectionBuffer && model.name === '_NAVIGATOR_') {
                const instanceId = model.selectionBuffer.getInstanceAt(
                    (x - (gl.canvas.width - this.NAVIGATION_VIEW_SIZE)) * (model.selectionBuffer.width / this.NAVIGATION_VIEW_SIZE),
                    (gl.canvas.height - y) * (model.selectionBuffer.height / this.NAVIGATION_VIEW_SIZE)
                );
                if (instanceId !== -1) {
                    const name = Module.getDecorationInstanceName(instanceId);
                    window.logInfo(`Selected Instance: ${name}, Model: ${model.name}`);
                    if (!name) {
                        return;
                    }

                    switch (name) {
                        case '#front':
                        case '#front-label':
                            this._rotateQuat = fromEulerXZ(-90, 180);
                            break;
                        case '#front-top-left':
                            this._rotateQuat = fromEulerXZ(-135, 225);
                            break;
                        case '#front-top-right':
                            this._rotateQuat = fromEulerXZ(-135, 135);
                            break;
                        case '#front-bottom-left':
                            this._rotateQuat = fromEulerXZ(-45, 225);
                            break;
                        case '#front-bottom-right':
                            this._rotateQuat = fromEulerXZ(-45, 135);
                            break;
                        case '#back':
                        case '#back-label':
                            this._rotateQuat = fromEulerXZ(-90, 0);
                            break;
                        case '#back-top-left':
                            this._rotateQuat = fromEulerXZ(-135, 45);
                            break;
                        case '#back-top-right':
                            this._rotateQuat = fromEulerXZ(-135, -45);
                            break;
                        case '#back-bottom-left':
                            this._rotateQuat = fromEulerXZ(-45, 45);
                            break;
                        case '#back-bottom-right':
                            this._rotateQuat = fromEulerXZ(-45, -45);
                            break;
                        case '#left':
                        case '#left-label':
                            this._rotateQuat = fromEulerXZ(-90, -90);
                            break;
                        case '#right':
                        case '#right-label':
                            this._rotateQuat = fromEulerXZ(-90, 90);
                            break;
                        case '#top':
                        case '#top-label':
                            this._rotateQuat = fromEulerXZ(-180, 180);
                            break;
                        case '#bottom':
                        case '#bottom-label':
                            this._rotateQuat = fromEulerXZ(0, 0);
                            break;
                    }

                    PENDING_DRAW_SCENE = true;
                }
            }
        }
    }

    /**
     * Select
     */
    Viewer.prototype.selectInstancesByID = function (IDs) {
        this._selectedInstances = [];

        if (!IDs || !Array.isArray(IDs)) {
            window.logErr("[Viewer.selectInstancesByID] Invalid IDs array.");
            return;
        }

        this._selectedInstances = [...IDs];

        PENDING_DRAW_SCENE = true;
    }

    /*
     * Highlight
     */
    Viewer.prototype.highlightInstancesByID = function (IDs) {
        this._highlightedInstances = [];

        if (!IDs || !Array.isArray(IDs)) {
            window.logErr("[Viewer.highlightInstancesByID] Invalid IDs array.");
            return;
        }

        this._highlightedInstances = [...IDs];

        PENDING_DRAW_SCENE = true;
    }

    /**
     * Visibility
     */
    Viewer.prototype.showInstancesByID = function (IDs, show) {
        try {
            if (!IDs || !Array.isArray(IDs)) {
                window.logErr("[Viewer.showInstancesByID] Invalid IDs array.");
                return;
            }

            if (IDs.length > 0) {
                for (let g = 0; g < this._geometries.length; g++) {
                    let geometry = this._geometries[g];
                    for (let i = 0; i < geometry.instances.length; i++) {
                        let instance = geometry.instances[i];
                        if (IDs.indexOf(instance.id) !== -1) {
                            instance.visible = show;
                        }
                    }
                }
            }
            else {
                for (let g = 0; g < this._geometries.length; g++) {
                    let geometry = this._geometries[g];
                    for (let i = 0; i < geometry.instances.length; i++) {
                        let instance = geometry.instances[i];
                        instance.visible = instance._visible && show;
                    }
                }
            }

            PENDING_DRAW_SCENE = true;
        }
        catch (e) {
            window.logErr(e);
        }
    }

    /**
     * Creates a WebGL texture and asynchronously loads an image from a file URL
     */
    Viewer.prototype.createTexture = function (textureFile) {
        try {
            var viewer = this;
            var texture = gl.createTexture();

            // Placeholder 1x1 black pixel while loading
            gl.bindTexture(gl.TEXTURE_2D, texture);
            gl.texImage2D(gl.TEXTURE_2D, 0, gl.RGBA, 1, 1, 0, gl.RGBA, gl.UNSIGNED_BYTE, new Uint8Array([0, 0, 0, 255]));

            fetch(textureFile)
                .then(function (response) { return response.blob(); })
                .then(function (blob) {
                    // Fix: Android/Samsung
                    return createImageBitmap(blob, { imageOrientation: 'flipY', premultiplyAlpha: 'none' });
                })
                .then(function (imageBitmap) {
                    gl.bindTexture(gl.TEXTURE_2D, texture);
                    // Fix: Android/Samsung
                    gl.pixelStorei(gl.UNPACK_PREMULTIPLY_ALPHA_WEBGL, false);
                    gl.texImage2D(gl.TEXTURE_2D, 0, gl.RGBA, gl.RGBA, gl.UNSIGNED_BYTE, imageBitmap);

                    if (viewer.isPowerOf2(imageBitmap.width) && viewer.isPowerOf2(imageBitmap.height)) {
                        gl.generateMipmap(gl.TEXTURE_2D);
                        gl.texParameteri(gl.TEXTURE_2D, gl.TEXTURE_MIN_FILTER, gl.LINEAR_MIPMAP_LINEAR);
                        gl.texParameteri(gl.TEXTURE_2D, gl.TEXTURE_MAG_FILTER, gl.LINEAR);
                        gl.texParameteri(gl.TEXTURE_2D, gl.TEXTURE_WRAP_S, gl.REPEAT);
                        gl.texParameteri(gl.TEXTURE_2D, gl.TEXTURE_WRAP_T, gl.REPEAT);
                    } else {
                        gl.texParameteri(gl.TEXTURE_2D, gl.TEXTURE_WRAP_S, gl.CLAMP_TO_EDGE);
                        gl.texParameteri(gl.TEXTURE_2D, gl.TEXTURE_WRAP_T, gl.CLAMP_TO_EDGE);
                        gl.texParameteri(gl.TEXTURE_2D, gl.TEXTURE_MIN_FILTER, gl.LINEAR);
                        gl.texParameteri(gl.TEXTURE_2D, gl.TEXTURE_MAG_FILTER, gl.LINEAR);
                    }

                    gl.bindTexture(gl.TEXTURE_2D, null);
                    imageBitmap.close();

                    PENDING_DRAW_SCENE = true;
                })
                .catch(function (e) {
                    window.logErr("[Viewer.createTexture] Can't load '" + textureFile + "': " + e);
                });

            return texture;
        } catch (e) {
            window.logErr(e);
        }
        return null;
    }

    /**
     * Creates a WebGL texture and asynchronously loads an image from base64-encoded data
     */
    Viewer.prototype.createTextureBase64 = function (base64Content) {
        try {
            var viewer = this;

            var texture = gl.createTexture();

            // Temp texture until the image is loaded
            // https://stackoverflow.com/questions/19722247/webgl-wait-for-texture-to-load/19748905#19748905
            gl.bindTexture(gl.TEXTURE_2D, texture)
            gl.texImage2D(
                gl.TEXTURE_2D,
                0,
                gl.RGBA,
                1,
                1,
                0,
                gl.RGBA,
                gl.UNSIGNED_BYTE,
                new Uint8Array([0, 0, 0, 255])
            );

            var image = new Image()
            image.addEventListener('error', function () {
                window.logErr("[Viewer.createTextureBase64] Can't load the texture.");
            })

            image.addEventListener('load', function () {
                gl.pixelStorei(gl.UNPACK_FLIP_Y_WEBGL, true);
                // Now that the image has loaded make copy it to the texture.
                gl.bindTexture(gl.TEXTURE_2D, texture);

                // Check if the image is a power of 2 in both dimensions.
                if (viewer.isPowerOf2(image.width) && viewer.isPowerOf2(image.height)) {
                    gl.texImage2D(
                        gl.TEXTURE_2D,
                        0,
                        gl.RGBA,
                        gl.RGBA,
                        gl.UNSIGNED_BYTE,
                        image
                    );

                    // Yes, it's a power of 2. Generate mips.
                    gl.generateMipmap(gl.TEXTURE_2D);
                    gl.texParameteri(
                        gl.TEXTURE_2D,
                        gl.TEXTURE_MIN_FILTER,
                        gl.LINEAR_MIPMAP_LINEAR
                    );
                } else {
                    // No, it's not a power of 2. Resize
                    image = viewer.makePowerOfTwo(image);
                    gl.texImage2D(
                        gl.TEXTURE_2D,
                        0,
                        gl.RGBA,
                        gl.RGBA,
                        gl.UNSIGNED_BYTE,
                        image);

                    gl.texParameteri(gl.TEXTURE_2D, gl.TEXTURE_MIN_FILTER, gl.LINEAR);
                }

                gl.bindTexture(gl.TEXTURE_2D, null);

                PENDING_DRAW_SCENE = true;
            });

            image.src = base64Content;

            return texture;
        } catch (e) {
            window.logErr(e)
        }

        return null;
    }

    /**
     * Texture support
     */
    Viewer.prototype.isPowerOf2 = function (value) {
        return (value & (value - 1)) === 0;
    }

    /**
     * Texture support
     */
    Viewer.prototype.floorPowerOfTwo = function (value) {
        return Math.pow(2, Math.floor(Math.log(value) / Math.LN2));
    }

    /**
     * Texture support
     */
    Viewer.prototype.makePowerOfTwo = function (image) {
        if (
            image instanceof HTMLImageElement ||
            image instanceof HTMLCanvasElement) {
            if (this._canvas === null)
                this._canvas = document.createElementNS(
                    'http://www.w3.org/1999/xhtml',
                    'canvas'
                )

            this._canvas.width = this.floorPowerOfTwo(image.width);
            this._canvas.height = this.floorPowerOfTwo(image.height);

            var context = this._canvas.getContext('2d');
            context.drawImage(image, 0, 0, this._canvas.width, this._canvas.height);

            return this._canvas;
        }

        return image;
    }

    /**
     * Zoom
     */
    Viewer.prototype.zoomTo = function (Xmin, Ymin, Zmin, Xmax, Ymax, Zmax) {
        try {
            window.logInfo(`[Viewer.zoomTo] Zooming to object with bounds: (${Xmin}, ${Ymin}, ${Zmin}) to (${Xmax}, ${Ymax}, ${Zmax})`);
            if (Xmin >= Xmax || Ymin >= Ymax || Zmin >= Zmax) {
                window.logErr("[Viewer.zoomTo] Invalid bounds provided. Zoom operation aborted.");
                return;
            }

            // Calculate center point of the target object
            const centerX = (Xmin + Xmax) / 2;
            const centerY = (Ymin + Ymax) / 2;
            const centerZ = (Zmin + Zmax) / 2;
            const objectCenter = [centerX, centerY, centerZ];
            window.logInfo(`[Viewer.zoomTo] Object center: (${centerX.toFixed(4)}, ${centerY.toFixed(4)}, ${centerZ.toFixed(4)})`);

            // Set rotation center to object center
            this._rotationCenter = vec3.fromValues(centerX, centerY, centerZ);

            // Calculate object dimensions
            const EPSILON = 0.0001;
            const width = Math.max(Xmax - Xmin, EPSILON);
            const height = Math.max(Ymax - Ymin, EPSILON);
            const depth = Math.max(Zmax - Zmin, EPSILON);
            window.logInfo(`[Viewer.zoomTo] Object dimensions: width=${width.toFixed(4)}, height=${height.toFixed(4)}, depth=${depth.toFixed(4)}`);

            // Calculate viewing distance based on object size
            const fov = 45 * (Math.PI / 180);
            const aspectRatio = gl.canvas.width / gl.canvas.height;
            let distance = Math.max(
                width / (2 * Math.tan(fov / 2)),
                height / (2 * Math.tan(fov / 2)) * aspectRatio,
                depth * 1.5
            );
            window.logInfo(`[Viewer.zoomTo] Calculated viewing distance: ${distance.toFixed(4)}`);

            // Define all faces with their directions and areas
            const faces = [
                { name: 'front', normal: [0, 0, -1], area: width * height },
                { name: 'back', normal: [0, 0, 1], area: width * height },
                { name: 'left', normal: [-1, 0, 0], area: depth * height },
                { name: 'right', normal: [1, 0, 0], area: depth * height },
                { name: 'bottom', normal: [0, -1, 0], area: width * depth },
                { name: 'top', normal: [0, 1, 0], area: width * depth }
            ];

            // Calculate a vector from origin to object center
            const vectorToObject = [centerX, centerY, centerZ];
            let distanceToObject = Math.sqrt(
                vectorToObject[0] * vectorToObject[0] +
                vectorToObject[1] * vectorToObject[1] +
                vectorToObject[2] * vectorToObject[2]
            );

            distanceToObject *= 1.5; // Adjust distance to ensure the object fits well in view

            let selectedFace;

            if (distanceToObject < EPSILON) {
                // Object is at origin, use largest face
                faces.sort((a, b) => b.area - a.area);
                selectedFace = faces[0];
            } else {
                // Normalize the vector
                const normalizedVector = [
                    vectorToObject[0] / distanceToObject,
                    vectorToObject[1] / distanceToObject,
                    vectorToObject[2] / distanceToObject
                ];

                // Find the face whose normal is most aligned with the vector from origin to object
                faces.forEach(face => {
                    face.alignment = -(
                        normalizedVector[0] * face.normal[0] +
                        normalizedVector[1] * face.normal[1] +
                        normalizedVector[2] * face.normal[2]
                    );
                });

                faces.sort((a, b) => b.alignment - a.alignment);
                selectedFace = faces[0];
                window.logInfo(`[Viewer.zoomTo] Selected face: ${selectedFace.name} with alignment=${selectedFace.alignment.toFixed(4)}`);
            }

            // Calculate simple look-at setup with no quaternion rotations
            this._rotateQuat = quat.create(); // Identity quaternion - no rotation
            this._targetVector = vec3.fromValues(objectCenter[0], objectCenter[1], objectCenter[2]);

            // Calculate view direction based on selected face
            const viewDir = [
                -selectedFace.normal[0],
                -selectedFace.normal[1],
                -selectedFace.normal[2]
            ];

            // Position camera at the desired distance in the view direction
            this._eyeVector = vec3.fromValues(
                objectCenter[0] + viewDir[0] * distance,
                objectCenter[1] + viewDir[1] * distance,
                objectCenter[2] + viewDir[2] * distance
            );

            // Choose appropriate up vector based on selected face
            if (selectedFace.name === 'front' || selectedFace.name === 'back') {
                this._upVector = vec3.fromValues(0, 1, 0);
            } else {
                this._upVector = vec3.fromValues(0, 0, 1);
            }

            window.logInfo(`[Viewer.zoomTo] Camera positioned at (${this._eyeVector[0].toFixed(4)}, ${this._eyeVector[1].toFixed(4)}, ${this._eyeVector[2].toFixed(4)})`);
            window.logInfo(`[Viewer.zoomTo] Looking at (${this._targetVector[0].toFixed(4)}, ${this._targetVector[1].toFixed(4)}, ${this._targetVector[2].toFixed(4)})`);
            window.logInfo(`[Viewer.zoomTo] Up vector: (${this._upVector[0].toFixed(4)}, ${this._upVector[1].toFixed(4)}, ${this._upVector[2].toFixed(4)})`);

            PENDING_DRAW_SCENE = true;
        } catch (e) {
            window.logErr(e);
        }
    }

    /**
     * Zoom
     */
    Viewer.prototype.zoomToXZ = function (Xmin, Ymin, Zmin, Xmax, Ymax, Zmax) {
        try {
            window.logInfo(`[Viewer.zoomToXZ] Zooming to object with bounds: (${Xmin}, ${Ymin}, ${Zmin}) to (${Xmax}, ${Ymax}, ${Zmax})`);
            if (Xmin > Xmax || Ymin > Ymax || Zmin > Zmax) {
                window.logErr("[Viewer.zoomToXZ] Invalid bounds provided. Zoom operation aborted.");
                return;
            }

            // Model center
            const modelCenterX = (this._worldDimensions.Xmin + this._worldDimensions.Xmax) / 2;
            const modelCenterY = (this._worldDimensions.Ymin + this._worldDimensions.Ymax) / 2;
            const modelCenterZ = (this._worldDimensions.Zmin + this._worldDimensions.Zmax) / 2;
            window.logInfo(`[Viewer.zoomToXZ] Model center: (${modelCenterX.toFixed(4)}, ${modelCenterY.toFixed(4)}, ${modelCenterZ.toFixed(4)})`);

            // Object center
            const objectCenterX = (Xmin + Xmax) / 2;
            const objectCenterY = (Ymin + Ymax) / 2;
            const objectCenterZ = (Zmin + Zmax) / 2;
            const objectCenter = [objectCenterX, objectCenterY, objectCenterZ];
            window.logInfo(`[Viewer.zoomToXZ] Object center: (${objectCenterX.toFixed(4)}, ${objectCenterY.toFixed(4)}, ${objectCenterZ.toFixed(4)})`);

            // Set rotation center to object center
            this._rotationCenter = vec3.fromValues(objectCenterX, objectCenterY, objectCenterZ);

            // Calculate object dimensions
            const EPSILON = 0.0001;
            const width = Math.max(Xmax - Xmin, EPSILON);
            const height = Math.max(Ymax - Ymin, EPSILON);
            const depth = Math.max(Zmax - Zmin, EPSILON);
            window.logInfo(`[Viewer.zoomToXZ] Object dimensions: width=${width.toFixed(4)}, height=${height.toFixed(4)}, depth=${depth.toFixed(4)}`);

            // Calculate viewing distance based on object size
            const fov = 45 * (Math.PI / 180);
            const aspectRatio = gl.canvas.width / gl.canvas.height;
            let distance = Math.max(
                width / (2 * Math.tan(fov / 2)),
                height / (2 * Math.tan(fov / 2)) * aspectRatio,
                depth * 1.5
            );
            window.logInfo(`[Viewer.zoomToXZ] Calculated viewing distance: ${distance.toFixed(4)}`);

            // Define all faces with their directions and areas
            let faces = [
                // Ignoring front/back faces for XZ view
                //{ name: 'front', normal: [0, 0, -1], area: width * height, eulerXZ: [-180, 0] },
                //{ name: 'back', normal: [0, 0, 1], area: width * height, eulerXZ: [0, 0] },
                { name: 'left', normal: [-1, 0, 0], area: depth * height, eulerXZ: [270, 90] },
                { name: 'right', normal: [1, 0, 0], area: depth * height, eulerXZ: [270, -90] },
                { name: 'bottom', normal: [0, -1, 0], area: width * depth, eulerXZ: [270, 0] },
                { name: 'top', normal: [0, 1, 0], area: width * depth, eulerXZ: [270, 180] }
            ];

            // Calculate vector from origin to object center
            const vectorToObject = [objectCenterX, objectCenterY, objectCenterZ];
            let distanceToObject = Math.sqrt(
                vectorToObject[0] * vectorToObject[0] +
                vectorToObject[1] * vectorToObject[1] +
                vectorToObject[2] * vectorToObject[2]
            );

            // Normal vector
            const normalizedVector = [
                vectorToObject[0] / distanceToObject,
                vectorToObject[1] / distanceToObject,
                vectorToObject[2] / distanceToObject
            ];

            faces.forEach(face => {
                // Calculate alignment (dot product of normal with vector to object)
                face.alignment = -(
                    normalizedVector[0] * face.normal[0] +
                    normalizedVector[1] * face.normal[1] +
                    normalizedVector[2] * face.normal[2]
                );
            });

            // Sort by area
            faces.sort((a, b) => b.area - a.area);
            window.logInfo(`[Viewer.zoomToXZ] Faces sorted by area:\n${faces.map(f => `  ${f.name}: alignment=${f.alignment.toFixed(4)}, area=${f.area.toFixed(4)}`).join('\n')}`);

            // Keep first 2 largest faces
            faces = faces.slice(0, 2);

            // Sort by alignment
            faces.sort((a, b) => b.alignment - a.alignment);
            window.logInfo(`[Viewer.zoomToXZ] Faces sorted by score:\n${faces.map(f => `  ${f.name}: alignment=${f.alignment.toFixed(4)}, area=${f.area.toFixed(4)}`).join('\n')}`);

            const selectedFace = faces[0];
            window.logInfo(`[Viewer.zoomToXZ] Selected face: ${selectedFace.name} (alignment=${selectedFace.alignment.toFixed(4)}, area=${selectedFace.area.toFixed(4)})`);

            // Set target to object center
            this._targetVector = vec3.fromValues(objectCenter[0], objectCenter[1], objectCenter[2]);

            // Position camera at standard location (along negative Z from actual object center)
            this._eyeVector = vec3.fromValues(
                objectCenter[0],
                objectCenter[1],
                objectCenter[2] - distance
            );

            // Set the quaternion to rotate the view to show the selected face
            this._rotateQuat = fromEulerXZ(selectedFace.eulerXZ[0], selectedFace.eulerXZ[1]);

            // Always use world up vector
            this._upVector = vec3.fromValues(0, 1, 0);

            window.logInfo(`[Viewer.zoomToXZ] Camera positioned at (${this._eyeVector[0].toFixed(4)}, ${this._eyeVector[1].toFixed(4)}, ${this._eyeVector[2].toFixed(4)})`);
            window.logInfo(`[Viewer.zoomToXZ] Looking at (${this._targetVector[0].toFixed(4)}, ${this._targetVector[1].toFixed(4)}, ${this._targetVector[2].toFixed(4)})`);
            window.logInfo(`[Viewer.zoomToXZ] Rotation center: (${this._rotationCenter[0].toFixed(4)}, ${this._rotationCenter[1].toFixed(4)}, ${this._rotationCenter[2].toFixed(4)})`);

            PENDING_DRAW_SCENE = true;
        } catch (e) {
            window.logErr(e);
        }
    }

    /**
     * Reset data and view
     */
    Viewer.prototype.reset = function () {
        try {
            this.deleteBuffers();

            this._model = {};
            this._geometries = [];
            this._id2instance = {};
            this._worldDimensions = { Xmin: -1.0, Ymin: -1.0, Zmin: -1.0, Xmax: 1.0, Ymax: 1.0, Zmax: 1.0, MaxDistance: 2.0 };
            this._scaleFactor = 1.0;

            this._mtxModelView = mat4.create();
            this._mtxProjection = mat4.create();
            this._mtxInversePMV = mat4.create();

            this.resetView(false);

            PENDING_DRAW_SCENE = true;
        } catch (e) {
            window.logErr(e)
        }
    }

    /*
     * Reset view
     */
    Viewer.prototype.resetView = function (redraw) {
        this._clearColor = [0.9, 0.9, 0.9, 1.0];
        this._pointLightPosition = vec3.fromValues(0.25, 0.25, 1);
        this._materialShininess = 50.0;
        this._defaultEyeVector = [0, 0, -5];
        this._eyeVector = vec3.fromValues(this._defaultEyeVector[0], this._defaultEyeVector[1], this._defaultEyeVector[2]);
        this._targetVector = vec3.fromValues(0, 0, 0);
        this._upVector = vec3.fromValues(0, 1, 0);
        //this._rotateQuat = fromEulerXZ(-135, 225); // X/Y
        this._rotateQuat = fromEulerXYZ(225, 45, 315); // X/Y/Z
        this._rotationCenter = vec3.fromValues(0, 0, 0);
        this._rotateAroundWorldCenter = false; // false => rotate around Model center/ZoomTo center

        const modelCenterX = (this._worldDimensions.Xmin + this._worldDimensions.Xmax) / 2;
        const modelCenterY = (this._worldDimensions.Ymin + this._worldDimensions.Ymax) / 2;
        const modelCenterZ = (this._worldDimensions.Zmin + this._worldDimensions.Zmax) / 2;

        this._targetVector = vec3.fromValues(modelCenterX, modelCenterY, modelCenterZ);
        if (this._rotateAroundWorldCenter) {
            // Rotation center at world center
            this._rotationCenter = vec3.fromValues(0, 0, 0);
        }
        else {
            // Rotation center at model center
            this._rotationCenter = vec3.fromValues(modelCenterX, modelCenterY, modelCenterZ);
        }

        this._defaultEyeVector[2] = -(2 * this._worldDimensions.MaxDistance);
        this._eyeVector = vec3.fromValues(this._defaultEyeVector[0], this._defaultEyeVector[1], this._defaultEyeVector[2]);

        this._showConceptualFaces = true;
        this._showConceptualFacesPolygons = false;
        this._showLines = true;
        this._showPoints = true;
        this._showModelCS = true;
        this._showWorldCS = false;
        this._showNavigator = true;

        for (let g = 0; g < this._geometries.length; g++) {
            let geometry = this._geometries[g];
            for (let i = 0; i < geometry.instances.length; i++) {
                let instance = geometry.instances[i];
                instance.visible = instance._visible;
            }
        }

        this._selectionBuffer.clearSelectionColorMap();
        this._selectedInstances = [];
        this._highlightedInstances = [];
        for (let m = 0; m < this._decorationModels.length; m++) {
            const model = this._decorationModels[m];
            if (model.selectionBuffer) {
                model.selectionBuffer.clearSelectionColorMap();
            }
        }

        if (redraw === undefined || redraw === true) {
            PENDING_DRAW_SCENE = true;
        }
    }

    /**************************************************************************
     * Instances
     */

    /**
     * Load
     */
    Viewer.prototype.loadDecorationModels = function (models) {
        window.logInfo('[Viewer.loadDecorationModels] BEGIN');

        this._decorationModels = [];
        if (!models || models.length === 0) {
            window.logInfo('[Viewer.loadDecorationModels] No decoration models to load.');
            return;
        }

        /*
        * Disable drawing
        */
        this._loadingModel = true;

        try {
            this._decorationModels = [...models];

            for (let m = 0; m < models.length; m++) {
                const model = models[m];
                model.id2instance = {};

                /*
                * WebGL buffers
                */
                for (let g = 0; g < model.geometries.length; g++) {
                    let geometry = model.geometries[g];

                    if (!geometry.vertices) {
                        window.logErr('[Viewer.loadDecorationModels] Unknown data model.');
                        continue;
                    }

                    for (let i = 0; i < geometry.instances.length; i++) {
                        model.id2instance[geometry.instances[i].id] = geometry.instances[i];
                    }

                    /*
                     * VBO-s
                     */
                    let vertexBufferObject = gl.createBuffer();
                    if (!vertexBufferObject) {
                        window.logErr('[Viewer.loadDecorationModels] Failed to create VBO.');
                        continue;
                    }
                    gl.bindBuffer(gl.ARRAY_BUFFER, vertexBufferObject);
                    gl.bufferData(
                        gl.ARRAY_BUFFER,
                        geometry.vertices, // WASM
                        //new Float32Array(geometry.vertices), // MAUI
                        gl.STATIC_DRAW
                    );
                    vertexBufferObject.length = geometry.vertices.length;
                    geometry.VBO = vertexBufferObject;

                    // IBOs - Conceptual faces
                    for (let f = 0; f < geometry.conceptualFaces.length; f++) {
                        if (geometry.conceptualFaces[f].indices &&
                            (geometry.conceptualFaces[f].indices.length > 0)) {
                            let indexBufferObject = gl.createBuffer();
                            if (!indexBufferObject) {
                                window.logErr('[Viewer.loadDecorationModels] Failed to create IBO.');
                                continue;
                            }
                            gl.bindBuffer(gl.ELEMENT_ARRAY_BUFFER, indexBufferObject);
                            gl.bufferData(
                                gl.ELEMENT_ARRAY_BUFFER,
                                geometry.conceptualFaces[f].indices, // WASM
                                //new Uint32Array(geometry.conceptualFaces[f].indices), // MAUI
                                gl.STATIC_DRAW
                            );
                            indexBufferObject.count = geometry.conceptualFaces[f].indices.length;
                            geometry.conceptualFaces[f].IBO = indexBufferObject;
                        }
                    } // for (let f = ...

                    // IBOs - Conceptual faces polygons
                    for (let j = 0; j < geometry.conceptualFacesPolygons.length; j++) {
                        if (geometry.conceptualFacesPolygons[j].indices &&
                            (geometry.conceptualFacesPolygons[j].indices.length > 0)) {
                            let indexBufferObject = gl.createBuffer();
                            if (!indexBufferObject) {
                                window.logErr('[Viewer.loadDecorationModels] Failed to create IBO.');
                            }
                            gl.bindBuffer(gl.ELEMENT_ARRAY_BUFFER, indexBufferObject)
                            gl.bufferData(
                                gl.ELEMENT_ARRAY_BUFFER,
                                geometry.conceptualFacesPolygons[j].indices, // WASM
                                //new Uint32Array(geometry.conceptualFacesPolygons[j].indices), // MAUI
                                gl.STATIC_DRAW
                            );
                            indexBufferObject.count = geometry.conceptualFacesPolygons[j].indices.length;
                            geometry.conceptualFacesPolygons[j].IBO = indexBufferObject;
                        }
                    }

                    // IBOs - Conceptual faces lines
                    for (let j = 0; j < geometry.conceptualFacesLines.length; j++) {
                        if (geometry.conceptualFacesLines[j].indices &&
                            (geometry.conceptualFacesLines[j].indices.length > 0)) {
                            let indexBufferObject = gl.createBuffer();
                            if (!indexBufferObject) {
                                window.logErr('[Viewer.loadDecorationModels] Failed to create IBO.');
                                continue;
                            }
                            gl.bindBuffer(gl.ELEMENT_ARRAY_BUFFER, indexBufferObject)
                            gl.bufferData(
                                gl.ELEMENT_ARRAY_BUFFER,
                                geometry.conceptualFacesLines[j].indices, // WASM
                                //new Uint32Array(geometry.conceptualFacesLines[j].indices), // MAUI
                                gl.STATIC_DRAW
                            );
                            indexBufferObject.count = geometry.conceptualFacesLines[j].indices.length;
                            geometry.conceptualFacesLines[j].IBO = indexBufferObject;
                        }
                    }

                    // IBOs - Conceptual faces points
                    for (let j = 0; j < geometry.conceptualFacesPoints.length; j++) {
                        if (geometry.conceptualFacesPoints[j].indices &&
                            (geometry.conceptualFacesPoints[j].indices.length > 0)) {
                            let indexBufferObject = gl.createBuffer();
                            if (!indexBufferObject) {
                                window.logErr('[Viewer.loadDecorationModels] Failed to create IBO.');
                                continue;
                            }
                            gl.bindBuffer(gl.ELEMENT_ARRAY_BUFFER, indexBufferObject)
                            gl.bufferData(
                                gl.ELEMENT_ARRAY_BUFFER,
                                geometry.conceptualFacesPoints[j].indices, // WASM
                                //new Uint32Array(geometry.conceptualFacesPoints[j].indices), // MAUI
                                gl.STATIC_DRAW
                            );
                            indexBufferObject.count = geometry.conceptualFacesPoints[j].indices.length;
                            geometry.conceptualFacesPoints[j].IBO = indexBufferObject;
                        }
                    }
                } // for (let g = ...

                /*
                * Selection buffer
                */
                if (model.name === '_NAVIGATOR_') {
                    model.selectionBuffer = new SelectionBuffer(gl);
                    model.selectionBuffer.initialize(this.SELECTION_BUFFER_SIZE, this.SELECTION_BUFFER_SIZE);
                }
            } // for (let m = 0; m < models.length; m++)
        } catch (e) {
            window.logErr(e);
        }

        /*
        * Enable drawing
        */
        this._loadingModel = false;

        PENDING_DRAW_SCENE = true;

        window.logInfo('[Viewer.loadDecorationModels] END');
    }

    /**
     * Load
     */
    Viewer.prototype.preLoadModel = function () {
        /*
        * Reset data and state
        */
        this.reset();

        /*
        * Disable drawing
        */
        this._loadingModel = true;
    }

    /**
     * Load
     */
    Viewer.prototype.loadModel = function (geometries) {
        window.logInfo('[Viewer.loadModel] BEGIN');

        try {
            this._geometries = [...geometries];
            this._id2instance = {};

            /*
            * WebGL buffers
            */
            for (let g = 0; g < this._geometries.length; g++) {
                let geometry = this._geometries[g];

                if (!geometry.vertices) {
                    window.logErr('[Viewer.loadModel] Unknown data model.');
                    continue;
                }

                for (let i = 0; i < geometry.instances.length; i++) {
                    this._id2instance[geometry.instances[i].id] = geometry.instances[i];
                }

                /*
                 * VBO-s
                 */
                let vertexBufferObject = gl.createBuffer()
                gl.bindBuffer(gl.ARRAY_BUFFER, vertexBufferObject);
                gl.bufferData(
                    gl.ARRAY_BUFFER,
                    geometry.vertices, // WASM
                    //new Float32Array(geometry.vertices), // MAUI
                    gl.STATIC_DRAW
                );
                vertexBufferObject.length = geometry.vertices.length;
                geometry.VBO = vertexBufferObject;

                // IBOs - Conceptual faces
                for (let f = 0; f < geometry.conceptualFaces.length; f++) {
                    if (geometry.conceptualFaces[f].indices &&
                        (geometry.conceptualFaces[f].indices.length > 0)) {
                        let indexBufferObject = gl.createBuffer();
                        gl.bindBuffer(gl.ELEMENT_ARRAY_BUFFER, indexBufferObject);
                        gl.bufferData(
                            gl.ELEMENT_ARRAY_BUFFER,
                            geometry.conceptualFaces[f].indices, // WASM
                            //new Uint32Array(geometry.conceptualFaces[f].indices), // MAUI
                            gl.STATIC_DRAW
                        );
                        indexBufferObject.count = geometry.conceptualFaces[f].indices.length;
                        geometry.conceptualFaces[f].IBO = indexBufferObject;
                    }
                } // for (let f = ...

                // IBOs - Conceptual faces polygons
                for (let j = 0; j < geometry.conceptualFacesPolygons.length; j++) {
                    if (geometry.conceptualFacesPolygons[j].indices &&
                        (geometry.conceptualFacesPolygons[j].indices.length > 0)) {
                        let indexBufferObject = gl.createBuffer();
                        gl.bindBuffer(gl.ELEMENT_ARRAY_BUFFER, indexBufferObject)
                        gl.bufferData(
                            gl.ELEMENT_ARRAY_BUFFER,
                            geometry.conceptualFacesPolygons[j].indices, // WASM
                            //new Uint32Array(geometry.conceptualFacesPolygons[j].indices), // MAUI
                            gl.STATIC_DRAW
                        );
                        indexBufferObject.count = geometry.conceptualFacesPolygons[j].indices.length;
                        geometry.conceptualFacesPolygons[j].IBO = indexBufferObject;
                    }
                }

                // IBOs - Conceptual faces lines
                for (let j = 0; j < geometry.conceptualFacesLines.length; j++) {
                    if (geometry.conceptualFacesLines[j].indices &&
                        (geometry.conceptualFacesLines[j].indices.length > 0)) {
                        let indexBufferObject = gl.createBuffer();
                        gl.bindBuffer(gl.ELEMENT_ARRAY_BUFFER, indexBufferObject)
                        gl.bufferData(
                            gl.ELEMENT_ARRAY_BUFFER,
                            geometry.conceptualFacesLines[j].indices, // WASM
                            //new Uint32Array(geometry.conceptualFacesLines[j].indices), // MAUI
                            gl.STATIC_DRAW
                        );
                        indexBufferObject.count = geometry.conceptualFacesLines[j].indices.length;
                        geometry.conceptualFacesLines[j].IBO = indexBufferObject;
                    }
                }

                // IBOs - Conceptual faces points
                for (let j = 0; j < geometry.conceptualFacesPoints.length; j++) {
                    if (geometry.conceptualFacesPoints[j].indices &&
                        (geometry.conceptualFacesPoints[j].indices.length > 0)) {
                        let indexBufferObject = gl.createBuffer();
                        gl.bindBuffer(gl.ELEMENT_ARRAY_BUFFER, indexBufferObject)
                        gl.bufferData(
                            gl.ELEMENT_ARRAY_BUFFER,
                            geometry.conceptualFacesPoints[j].indices, // WASM
                            //new Uint32Array(geometry.conceptualFacesPoints[j].indices), // MAUI
                            gl.STATIC_DRAW
                        );
                        indexBufferObject.count = geometry.conceptualFacesPoints[j].indices.length;
                        geometry.conceptualFacesPoints[j].IBO = indexBufferObject;
                    }
                }
            } // for (let g = ...

            /*
            * Camera Target/Rotation center
            */
            const modelCenterX = (this._worldDimensions.Xmin + this._worldDimensions.Xmax) / 2;
            const modelCenterY = (this._worldDimensions.Ymin + this._worldDimensions.Ymax) / 2;
            const modelCenterZ = (this._worldDimensions.Zmin + this._worldDimensions.Zmax) / 2;

            // Set target to model center
            this._targetVector = vec3.fromValues(modelCenterX, modelCenterY, modelCenterZ);
            if (this._rotateAroundWorldCenter) {
                // Rotation center at world center
                this._rotationCenter = vec3.fromValues(0, 0, 0);
            }
            else {
                // Rotation center at model center
                this._rotationCenter = vec3.fromValues(modelCenterX, modelCenterY, modelCenterZ);
                window.logInfo(`[Viewer.loadModel] Model center set to: (${modelCenterX.toFixed(4)}, ${modelCenterY.toFixed(4)}, ${modelCenterZ.toFixed(4)})`);
            }

            /*
            * Eye
            */
            this._defaultEyeVector[2] = -(2 * this._worldDimensions.MaxDistance);
            this._eyeVector = vec3.fromValues(this._defaultEyeVector[0], this._defaultEyeVector[1], this._defaultEyeVector[2]);
        } catch (e) {
            window.logErr(e);
        }

        /*
        * Enable drawing
        */
        this._loadingModel = false;

        PENDING_DRAW_SCENE = true;

        window.logInfo('[Viewer.loadModel] END');
    }

    /**
    * Textures
    */
    Viewer.prototype.getTexture = function (textureName) {
        if (this._textures[textureName]) {
            return this._textures[textureName];
        }
        return this._defaultTexture;
    }

    /**
     * Cleanup
     */
    Viewer.prototype.deleteBuffers = function () {
        try {
            for (let g = 0; g < this._geometries.length; g++) {
                let geometry = this._geometries[g]

                if (geometry.VBO) {
                    gl.deleteBuffer(geometry.VBO);
                }

                for (let f = 0; f < geometry.conceptualFaces.length; f++) {
                    if (geometry.conceptualFaces[f].IBO) {
                        gl.deleteBuffer(geometry.conceptualFaces[f].IBO);
                    }
                }

                for (let j = 0; j < geometry.conceptualFacesPolygons.length; j++) {
                    if (geometry.conceptualFacesPolygons[j].IBO) {
                        gl.deleteBuffer(geometry.conceptualFacesPolygons[j].IBO);
                    }
                }

                for (let j = 0; j < geometry.conceptualFacesLines.length; j++) {
                    if (geometry.conceptualFacesLines[j].IBO) {
                        gl.deleteBuffer(geometry.conceptualFacesLines[j].IBO);
                    }
                }

                for (let j = 0; j < geometry.conceptualFacesPoints.length; j++) {
                    if (geometry.conceptualFacesPoints[j].IBO) {
                        gl.deleteBuffer(geometry.conceptualFacesPoints[j].IBO);
                    }
                }

                Object.keys(this._textures).forEach(textureName => {
                    gl.deleteTexture(this._textures[textureName]);
                });
                this._textures = {};

                this._canvas = null;
            } // for (let g = ...
        } catch (e) {
            window.logErr(e);
        }
    }

    /**
     * Draw
     */
    Viewer.prototype.drawInstances = function () {
        this.drawConceptualFaces(this._geometries, false);
        this.drawConceptualFaces(this._geometries, true);
        this.drawConceptualFacesPolygons(this._geometries);
        this.drawLines(this._geometries);
        this.drawPoints(this._geometries);

        const showConceptualFaces = this._showConceptualFaces;
        const showConceptualFacesPolygons = this._showConceptualFacesPolygons;
        const showLines = this._showLines;
        const showPoints = this._showPoints;
        this._showConceptualFaces = true;
        this._showConceptualFacesPolygons = true;
        this._showLines = true;
        this._showPoints = true;
        for (let m = 0; m < this._decorationModels.length; m++) {
            const decorationModel = this._decorationModels[m];

            /*
            * Set viewport and projection/model-view matrices
            */
            if (decorationModel.name === '_MODEL_COORDINATE_SYSTEM_') {
                if (!this._showModelCS) {
                    continue;
                }
                this.setProjectionMatrix(gl.canvas.width, gl.canvas.height);
                gl.viewport(0, 0, gl.canvas.width, gl.canvas.height);

                this.setModelViewMatrix();
            }
            else if (decorationModel.name === '_WORLD_COORDINATE_SYSTEM_') {
                if (!this._showWorldCS) {
                    continue;
                }
                this.setProjectionMatrix(gl.canvas.width, gl.canvas.height);
                gl.viewport(0, 0, gl.canvas.width, gl.canvas.height);

                this.setModelViewMatrix(false);
            } else if (decorationModel.name === '_NAVIGATOR_') {
                if (!this._showNavigator) {
                    continue;
                }
                this.setProjectionMatrix(this.NAVIGATION_VIEW_SIZE, this.NAVIGATION_VIEW_SIZE);
                gl.viewport(gl.canvas.width - this.NAVIGATION_VIEW_SIZE, 0, this.NAVIGATION_VIEW_SIZE, this.NAVIGATION_VIEW_SIZE);

                this.setModelViewMatrix(false);
            } else {
                this.setProjectionMatrix(gl.canvas.width, gl.canvas.height);
                gl.viewport(0, 0, gl.canvas.width, gl.canvas.height);

                this.setModelViewMatrix();
            }

            this.drawConceptualFaces(decorationModel.geometries, false);
            this.drawConceptualFaces(decorationModel.geometries, true);
            this.drawConceptualFacesPolygons(decorationModel.geometries);
            this.drawLines(decorationModel.geometries);
            this.drawPoints(decorationModel.geometries);
        }
        this._showConceptualFaces = showConceptualFaces;
        this._showConceptualFacesPolygons = showConceptualFacesPolygons;
        this._showLines = showLines;
        this._showPoints = showPoints;
    }

    /**
     * VBO
     */
    Viewer.prototype.setVBO = function (geometry) {
        if (!geometry || !geometry.VBO) {
            return false;
        }

        gl.bindBuffer(gl.ARRAY_BUFFER, geometry.VBO);
        gl.vertexAttribPointer(
            this._shaderProgram.VertexPosition,
            3,
            gl.FLOAT,
            false,
            geometry.vertexSizeInBytes,
            0
        );
        gl.enableVertexAttribArray(this._shaderProgram.VertexPosition);

        gl.vertexAttribPointer(
            this._shaderProgram.VertexNormal,
            3,
            gl.FLOAT,
            true,
            geometry.vertexSizeInBytes,
            12
        );
        gl.enableVertexAttribArray(this._shaderProgram.VertexNormal);

        if (geometry.vertexSizeInBytes > 24) {
            gl.vertexAttribPointer(
                this._shaderProgram.UV,
                2,
                gl.FLOAT,
                false,
                geometry.vertexSizeInBytes,
                24
            );
            gl.enableVertexAttribArray(this._shaderProgram.UV);
        }

        return true;
    }

    /**
     * Transformation
     */
    Viewer.prototype.applyTransformationMatrix = function (matrix) {
        if (matrix && matrix.length === 16) {
            // Model-View matrix
            mat4.identity(this._mtxModelView);
            mat4.lookAt(
                this._mtxModelView,
                this._eyeVector,
                this._targetVector,
                this._upVector
            );

            // Determine rotation center based on mode
            const rotationCenter = this._rotateAroundWorldCenter
                ? vec3.fromValues(this._targetVector[0], this._targetVector[1], this._targetVector[2]) // World origin
                : this._rotationCenter; // Model center/ZoomTo center

            // Create a matrix to move the rotation center to origin
            const centerToOrigin = mat4.create();
            mat4.identity(centerToOrigin);
            mat4.translate(
                centerToOrigin,
                centerToOrigin,
                vec3.fromValues(
                    -rotationCenter[0],
                    -rotationCenter[1],
                    -rotationCenter[2]
                )
            );

            // Create quaternion rotation matrix
            const rotationMatrix = mat4.create();
            mat4.fromQuat(rotationMatrix, this._rotateQuat);

            // Create matrix to move back from origin to rotation center
            const originToCenter = mat4.create();
            mat4.identity(originToCenter);
            mat4.translate(
                originToCenter,
                originToCenter,
                vec3.fromValues(
                    rotationCenter[0],
                    rotationCenter[1],
                    rotationCenter[2]
                )
            );

            // Apply the transformations: move to origin, rotate, move back
            const transformMatrix = mat4.create();
            mat4.multiply(transformMatrix, originToCenter, rotationMatrix);
            mat4.multiply(transformMatrix, transformMatrix, centerToOrigin);

            // Apply the view rotation transformation
            mat4.multiply(this._mtxModelView, this._mtxModelView, transformMatrix);

            // Apply the instance transformation matrix (instance transform is applied first in world space)
            var mtxTransformation = mat4.clone(matrix);
            mat4.multiply(this._mtxModelView, this._mtxModelView, mtxTransformation);

            gl.uniformMatrix4fv(
                this._shaderProgram.ModelViewMatrix,
                false,
                this._mtxModelView
            );

            // Normal matrix
            var normalMatrix = mat3.create();
            mat3.fromMat4(normalMatrix, this._mtxModelView);
            gl.uniformMatrix3fv(this._shaderProgram.NormalMatrix, false, normalMatrix);

            return true;
        }

        return false;
    }

    /**
     * Triangles
     */
    Viewer.prototype.drawConceptualFaces = function (geometries, transparent) {
        if (!this._showConceptualFaces) {
            return;
        }

        if (geometries.length === 0) {
            return;
        }

        gl.uniform1f(this._shaderProgram.EnableLighting, 1.0);
        gl.uniform1f(this._shaderProgram.EnableTexture, 0.0);
        gl.uniform1f(this._shaderProgram.Transparency, 1.0);

        if (transparent) {
            gl.enable(gl.BLEND);
            gl.blendEquation(gl.FUNC_ADD);
            gl.blendFunc(gl.SRC_ALPHA, gl.ONE_MINUS_SRC_ALPHA);
        }

        try {
            for (let g = 0; g < geometries.length; g++) {
                let geometry = geometries[g];
                if (!geometry.conceptualFaces) {
                    continue;
                }

                if (!this.setVBO(geometry)) {
                    window.logErr('[Viewer.drawConceptualFaces] setVBO: Internal error!');
                    continue;
                }

                for (let i = 0; i < geometry.instances.length; i++) {
                    let instance = geometry.instances[i];
                    if (!instance.visible) {
                        continue;
                    }

                    let restoreModelViewMatrix = this.applyTransformationMatrix(instance.matrix);

                    let instanceSelected = this._selectedInstances.indexOf(instance.id) !== -1;
                    let instanceHighlighted = this._highlightedInstances.indexOf(instance.id) !== -1;
                    for (let f = 0; f < geometry.conceptualFaces.length; f++) {
                        let conceptualFace = geometry.conceptualFaces[f];
                        if (!conceptualFace.IBO) {
                            continue;
                        }

                        let material = instanceSelected ? SELECTED_INSTANCE_MATERIAL :
                            instanceHighlighted ? HIGHLIGHTED_INSTANCE_MATERIAL : conceptualFace.material;
                        if (transparent) {
                            if (material.transparency >= 1.0) {
                                continue;
                            }
                        } else {
                            if (material.transparency < 1.0) {
                                continue;
                            }
                        }

                        if (material.texture) {
                            gl.uniform1f(this._shaderProgram.EnableTexture, 1.0);
                            gl.uniform1f(this._shaderProgram.Transparency, material.transparency);
                            gl.activeTexture(gl.TEXTURE0);
                            gl.bindTexture(
                                gl.TEXTURE_2D,
                                this.getTexture(material.texture.name)
                            );
                            gl.uniform1i(this._shaderProgram.Sampler, 0);
                        } else {
                            gl.uniform1f(this._shaderProgram.EnableTexture, 0.0);
                            gl.uniform3f(
                                this._shaderProgram.AmbientMaterial,
                                material.ambient[0],
                                material.ambient[1],
                                material.ambient[2]
                            );
                            gl.uniform3f(
                                this._shaderProgram.DiffuseMaterial,
                                material.diffuse[0],
                                material.diffuse[1],
                                material.diffuse[2]
                            );
                            gl.uniform3f(
                                this._shaderProgram.SpecularMaterial,
                                material.specular[0],
                                material.specular[1],
                                material.specular[2]
                            );
                            gl.uniform1f(
                                this._shaderProgram.Transparency,
                                material.transparency);
                        }

                        gl.bindBuffer(gl.ELEMENT_ARRAY_BUFFER, conceptualFace.IBO);
                        gl.drawElements(
                            gl.TRIANGLES,
                            conceptualFace.IBO.count,
                            gl.UNSIGNED_INT,
                            0
                        );
                    } // for (let f = ...

                    if (restoreModelViewMatrix) {
                        this.setModelViewMatrix();
                    }
                } // for (let i = ...
            } // for (let g = ...
        } catch (e) {
            window.logErr(e)
        }

        // Always reset texture state after drawing
        gl.uniform1f(this._shaderProgram.EnableTexture, 0.0);
        gl.bindTexture(gl.TEXTURE_2D, null);

        if (transparent) {
            gl.disable(gl.BLEND);
        }
    }

    /**
     * Conceptual faces polygons
     */
    Viewer.prototype.drawConceptualFacesPolygons = function (geometries) {
        if (!this._showConceptualFacesPolygons) {
            return;
        }

        if (geometries.length === 0) {
            return;
        }

        gl.uniform1f(this._shaderProgram.EnableLighting, 0.0);
        gl.uniform1f(this._shaderProgram.EnableTexture, 0.0);

        gl.uniform3f(this._shaderProgram.AmbientMaterial, 0.0, 0.0, 0.0);
        gl.uniform3f(this._shaderProgram.SpecularMaterial, 0.0, 0.0, 0.0);
        gl.uniform3f(this._shaderProgram.DiffuseMaterial, 0.0, 0.0, 0.0);
        // #todo
        //gl.uniform3f(this._shaderProgram.uMaterialEmissiveColor, 0.0, 0.0, 0.0) 
        gl.uniform1f(this._shaderProgram.Transparency, 1.0);

        try {
            for (let g = 0; g < geometries.length; g++) {
                let geometry = geometries[g];
                if (!geometry.conceptualFacesPolygons) {
                    continue;
                }

                if (!this.setVBO(geometry)) {
                    window.logErr('[Viewer.drawConceptualFacesPolygons] setVBO: Internal error!');
                    continue;
                }

                for (let i = 0; i < geometry.instances.length; i++) {
                    let instance = geometry.instances[i];
                    if (!instance.visible) {
                        continue;
                    }

                    let restoreModelViewMatrix = this.applyTransformationMatrix(instance.matrix);

                    for (let f = 0; f < geometry.conceptualFacesPolygons.length; f++) {
                        let conceptualFacesPolygons = geometry.conceptualFacesPolygons[f]
                        if (!conceptualFacesPolygons.IBO) {
                            continue;
                        }

                        gl.bindBuffer(gl.ELEMENT_ARRAY_BUFFER, conceptualFacesPolygons.IBO)
                        gl.drawElements(
                            gl.LINES,
                            conceptualFacesPolygons.IBO.count,
                            gl.UNSIGNED_INT,
                            0);
                    } // for (let f = ...

                    if (restoreModelViewMatrix) {
                        this.setModelViewMatrix();
                    }
                } // for (let i = ...
            } // for (let g = ...
        } catch (e) {
            window.logErr(e)
        }
    }

    /**
     * Lines
     */
    Viewer.prototype.drawLines = function (geometries) {
        if (!this._showLines) {
            return
        }

        if (geometries.length === 0) {
            return;
        }

        gl.uniform1f(this._shaderProgram.EnableLighting, 0.0);
        gl.uniform1f(this._shaderProgram.EnableTexture, 0.0);

        gl.uniform3f(this._shaderProgram.AmbientMaterial, 0.0, 0.0, 0.0);
        gl.uniform3f(this._shaderProgram.SpecularMaterial, 0.0, 0.0, 0.0);
        gl.uniform3f(this._shaderProgram.DiffuseMaterial, 0.0, 0.0, 0.0);
        // #todo
        //gl.uniform3f(this._shaderProgram.uMaterialEmissiveColor, 0.0, 0.0, 0.0);
        gl.uniform1f(this._shaderProgram.Transparency, 1.0);

        try {
            for (let g = 0; g < geometries.length; g++) {
                let geometry = geometries[g];
                if (!geometry.conceptualFacesLines) {
                    continue;
                }

                if (!this.setVBO(geometry)) {
                    window.logErr('[Viewer.drawLines] setVBO: Internal error!');
                    continue;
                }

                for (let i = 0; i < geometry.instances.length; i++) {
                    let instance = geometry.instances[i];
                    if (!instance.visible) {
                        continue;
                    }

                    let restoreModelViewMatrix = this.applyTransformationMatrix(instance.matrix);

                    for (let f = 0; f < geometry.conceptualFacesLines.length; f++) {
                        let conceptualFacesLines = geometry.conceptualFacesLines[f]
                        if (!conceptualFacesLines.IBO) {
                            continue;
                        }

                        gl.uniform3f(this._shaderProgram.AmbientMaterial,
                            conceptualFacesLines.color[0],
                            conceptualFacesLines.color[1],
                            conceptualFacesLines.color[2]);

                        gl.bindBuffer(gl.ELEMENT_ARRAY_BUFFER, conceptualFacesLines.IBO)
                        gl.drawElements(
                            gl.LINES,
                            conceptualFacesLines.IBO.count,
                            gl.UNSIGNED_INT,
                            0);
                    } // for (let f = ...

                    if (restoreModelViewMatrix) {
                        this.setModelViewMatrix();
                    }
                } // for (let i = ...
            } // for (let g = ...
        } catch (e) {
            window.logErr(e);
        }
    }

    /**
     * Points
     */
    Viewer.prototype.drawPoints = function (geometries) {
        if (!this._showPoints) {
            return
        }

        if (geometries.length === 0) {
            return;
        }

        gl.uniform1f(this._shaderProgram.EnableLighting, 0.0);
        gl.uniform1f(this._shaderProgram.EnableTexture, 0.0);

        gl.uniform3f(this._shaderProgram.AmbientMaterial, 0.0, 0.0, 0.0);
        gl.uniform3f(this._shaderProgram.SpecularMaterial, 0.0, 0.0, 0.0);
        gl.uniform3f(this._shaderProgram.DiffuseMaterial, 0.0, 0.0, 0.0);
        // #todo
        //gl.uniform3f(this._shaderProgram.uMaterialEmissiveColor, 0.0, 0.0, 0.0);
        gl.uniform1f(this._shaderProgram.Transparency, 1.0);

        try {
            for (let g = 0; g < geometries.length; g++) {
                let geometry = geometries[g];
                if (!geometry.conceptualFacesPoints) {
                    continue;
                }

                if (!this.setVBO(geometry)) {
                    window.logErr('[Viewer.drawPoints] setVBO: Internal error!');
                    continue;
                }

                for (let i = 0; i < geometry.instances.length; i++) {
                    let instance = geometry.instances[i];
                    if (!instance.visible) {
                        continue;
                    }

                    let restoreModelViewMatrix = this.applyTransformationMatrix(instance.matrix);

                    for (let f = 0; f < geometry.conceptualFacesPoints.length; f++) {
                        let conceptualFacesPoints = geometry.conceptualFacesPoints[f]
                        if (!conceptualFacesPoints.IBO) {
                            continue;
                        }

                        gl.uniform3f(this._shaderProgram.AmbientMaterial,
                            conceptualFacesPoints.color[0],
                            conceptualFacesPoints.color[1],
                            conceptualFacesPoints.color[2]);

                        gl.bindBuffer(gl.ELEMENT_ARRAY_BUFFER, conceptualFacesPoints.IBO)
                        gl.drawElements(
                            gl.POINTS,
                            conceptualFacesPoints.IBO.count,
                            gl.UNSIGNED_INT,
                            0);
                    } // for (let f = ...

                    if (restoreModelViewMatrix) {
                        this.setModelViewMatrix();
                    }
                } // for (let i = ...
            } // for (let g = ...
        } catch (e) {
            window.logErr(e);
        }
    }

    /**
     * Selection support
     */
    Viewer.prototype.drawInstancesSelectionBuffer = function (geometries, selectionBuffer) {
        if (geometries.length === 0 || !selectionBuffer) {
            return;
        }

        try {
            selectionBuffer.buildSelectionColorMap(geometries);

            gl.uniform1f(this._shaderProgram.EnableLighting, 0.0);
            gl.uniform1f(this._shaderProgram.EnableTexture, 0.0);
            gl.uniform1f(this._shaderProgram.Transparency, 1.0);

            selectionBuffer.bind();
            gl.viewport(
                0,
                0,
                selectionBuffer.width,
                selectionBuffer.height
            );
            gl.clearColor(0.0, 0.0, 0.0, 0.0);
            gl.clear(gl.COLOR_BUFFER_BIT | gl.DEPTH_BUFFER_BIT);

            for (let g = 0; g < geometries.length; g++) {
                let geometry = geometries[g];
                if (!geometry.conceptualFaces) {
                    continue;
                }

                if (!this.setVBO(geometry)) {
                    window.logErr('[Viewer.drawInstancesSelectionBuffer] setVBO: Internal error!');
                    continue;
                }

                for (let i = 0; i < geometry.instances.length; i++) {
                    let instance = geometry.instances[i];
                    if (!instance.visible) {
                        continue;
                    }

                    let restoreModelViewMatrix = this.applyTransformationMatrix(instance.matrix);

                    gl.uniform3f(
                        this._shaderProgram.AmbientMaterial,
                        selectionBuffer.colorMap[instance.id][0],
                        selectionBuffer.colorMap[instance.id][1],
                        selectionBuffer.colorMap[instance.id][2]
                    );

                    for (let f = 0; f < geometry.conceptualFaces.length; f++) {
                        let conceptualFace = geometry.conceptualFaces[f];
                        if (!conceptualFace.IBO) {
                            continue;
                        }

                        gl.bindBuffer(gl.ELEMENT_ARRAY_BUFFER, conceptualFace.IBO);
                        gl.drawElements(
                            gl.TRIANGLES,
                            conceptualFace.IBO.count,
                            gl.UNSIGNED_INT,
                            0
                        );
                    } // for (let f = ...

                    if (restoreModelViewMatrix) {
                        this.setModelViewMatrix();
                    }
                } // for (let i = ...
            } // for (let g = ...

            selectionBuffer.unbind();
        } catch (e) {
            window.logErr(e)
        }
    }
}

/**
 * Viewer
 */
var g_viewer = new Viewer()

/**
 * Initialize
 */
function initializeWebGLViewer() {
    setTimeout(() => {
        try {
            window.logInfo("[initializeWebGLViewer] Attempting to initialize WebGL Viewer...");
            const canvas = document.getElementById('canvas-element-id')
            const width = canvas.width || 300;
            const height = canvas.height || 300;
            g_viewer.init('canvas-element-id', width, height);
            window.logInfo("[initializeWebGLViewer] WebGL Viewer initialized successfully.");

            const maxElementsIndices = gl.getParameter(gl.MAX_ELEMENTS_INDICES);
            const maxElementsVertices = gl.getParameter(gl.MAX_ELEMENTS_VERTICES);
            window.logInfo(`[initializeWebGLViewer] MAX_ELEMENTS_INDICES: ${maxElementsIndices}`);
            window.logInfo(`[initializeWebGLViewer] MAX_ELEMENTS_VERTICES: ${maxElementsVertices}`);

            // Custom event handler
            if (g_onWebGLInitializedEvent !== null) {
                g_onWebGLInitializedEvent();
            }
        } catch (e) {
            window.logErr(e);
        }
    }, 500);
}

/**
 * Render
 */
function renderLoop() {
    utils.requestAnimFrame(renderLoop)

    if (!PENDING_DRAW_SCENE) {
        return;
    }

    g_viewer.drawScene();

    PENDING_DRAW_SCENE = false;
}


