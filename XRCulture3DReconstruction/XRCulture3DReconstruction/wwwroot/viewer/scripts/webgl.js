/*
 * Global variables
 */
var DRAW_SCENE_INTERVAL = 40;
var PENDING_DRAW_SCENE = false;

/**
* Canvas resize handler
*/
function resizeCanvas() {
    const canvas = document.getElementById('canvas-element-id');
    const container = document.getElementById('canvas_container');

    // Set canvas size to match container
    canvas.width = container.clientWidth;
    canvas.height = container.clientHeight;

    PENDING_DRAW_SCENE = true;
}

/**
* Add resize listener
*/
window.addEventListener('resize', resizeCanvas);

/**
* Initial sizing and when orientation changes
*/
window.addEventListener('orientationchange', resizeCanvas);

/**
* Call on page load
*/
document.addEventListener('DOMContentLoaded', function () {
    resizeCanvas();
    // Call again after a slight delay to handle any layout adjustments
    setTimeout(resizeCanvas, 100);
});

/**
* Utility functions for WebGL
*/
function utilsObject() { }
var utils = new utilsObject();

/**
* Shader sources for the WebGL application.
*/
const SHADERS = {
    'shader-vs': `attribute vec4 Position;
        attribute vec3 Normal;
        attribute vec2 UV;

        uniform mat4 ProjectionMatrix;
        uniform mat4 ModelViewMatrix;
        uniform mat3 NormalMatrix;
        uniform vec3 DiffuseMaterial;
        uniform float EnableLighting;
        uniform float EnableTexture;

        varying vec3 _EyespaceNormal;
        varying vec3 _Diffuse;
        varying float _EnableLighting;
        varying float _EnableTexture;
        varying vec2 _UV;

        void main()
        {
            if (EnableLighting > 0.5)
            {
                _EyespaceNormal = NormalMatrix * Normal;
                _Diffuse = DiffuseMaterial;
            }

            _EnableLighting = EnableLighting;
            _EnableTexture = EnableTexture;
            _UV = UV;

            gl_Position = ProjectionMatrix * ModelViewMatrix * Position;
            gl_PointSize = 5.0;
        }`,

    'shader-fs': `precision highp float;

        varying vec3 _EyespaceNormal;
        varying vec3 _Diffuse;
        varying float _EnableLighting;
        varying float _EnableTexture;
        varying vec2 _UV;

        uniform vec3 LightPosition;
        uniform vec3 AmbientMaterial;
        uniform vec3 SpecularMaterial;
        uniform float Transparency;
        uniform float Shininess;
        uniform vec3 AmbientLightWeighting;
        uniform vec3 DiffuseLightWeighting;
        uniform vec3 SpecularLightWeighting;
        uniform sampler2D Sampler;

        void main()
        {
            // Texture rendering path
            if (_EnableTexture > 0.5) {
                gl_FragColor = texture2D(Sampler, _UV);
                return;
            }

            // Lighting path
            vec3 color = AmbientMaterial;
            if (_EnableLighting > 0.5) {
                vec3 N = normalize(_EyespaceNormal);
                vec3 L = normalize(LightPosition);
                vec3 E = vec3(0.0, 0.0, 1.0);
                vec3 H = normalize(L + E);

                float df = max(dot(N, L), 0.0);
                float sf = pow(max(dot(N, H), 0.0), Shininess);

                color =
                    (AmbientMaterial * AmbientLightWeighting) +
                    (df * _Diffuse * DiffuseLightWeighting) +
                    (sf * SpecularMaterial * SpecularLightWeighting);
            }

            gl_FragColor = vec4(color, Transparency);
        }`
};

/**
* Obtains a WebGL context for the canvas with id 'canvas-element-id'
* This function is invoked when the WebGL app is starting.
*/
utilsObject.prototype.getGLContext = function (name) {

    var canvas = document.getElementById(name);
    var ctx = null;

    if (canvas == null) {
        window.logErr('There is no canvas on this page.');
        return null;
    }

    const contextOptions = {
        alpha: false,
        antialias: true,
        depth: true,
        failIfMajorPerformanceCaveat: false,
        powerPreference: "high-performance",
        premultipliedAlpha: false,
        preserveDrawingBuffer: true, // Needed for screenshots
        stencil: false
    };

    var names = ["webgl2", "webgl", "experimental-webgl", "webkit-3d", "moz-webgl"];
    for (var i = 0; i < names.length; ++i) {
        try {
            ctx = canvas.getContext(names[i], contextOptions);
            window.logInfo(`[utilsObject.getGLContext] WebGL Context: ${names[i]}`);
        }
        catch (e) { }
        if (ctx) {
            break;
        }
    }

    if (ctx == null) {
        window.logErr('Could not initialise WebGL.');
        return null;
    }

    // Set viewport size to improve mobile performance
    const pixelRatio = Math.min(window.devicePixelRatio, 2);
    const displayWidth = Math.floor(canvas.clientWidth * pixelRatio);
    const displayHeight = Math.floor(canvas.clientHeight * pixelRatio);

    if (canvas.width !== displayWidth || canvas.height !== displayHeight) {
        canvas.width = displayWidth;
        canvas.height = displayHeight;
    }

    // Enable extensions that improve performance
    ctx.getExtension('OES_element_index_uint');
    ctx.getExtension('WEBGL_depth_texture');

    // Optimize WebGL state changes
    ctx.hint(ctx.GENERATE_MIPMAP_HINT, ctx.FASTEST);
    ctx.disable(ctx.DITHER);

    return ctx;
}

/**
* Utilitary function that allows to set up the shaders (program) using an embedded script (look at the beginning of this source code)
*/
utilsObject.prototype.getShader = function (gl, id) {
    try {
        if (SHADERS[id]) {
            const shaderType = id.includes("-vs") ? gl.VERTEX_SHADER : gl.FRAGMENT_SHADER;
            const shader = gl.createShader(shaderType);

            gl.shaderSource(shader, SHADERS[id]);
            gl.compileShader(shader);

            if (!gl.getShaderParameter(shader, gl.COMPILE_STATUS)) {
                window.logErr(`Error compiling shader ${id}: ${gl.getShaderInfoLog(shader)}`);
                return null;
            }
            return shader;
        }
        window.logErr(`Shader ${id} not found.`);
        return null;
    } catch (e) {
        window.logErr(e);
        return null;
    }
}

/**
* Provides requestAnimationFrame in a cross browser way.
*/
utilsObject.prototype.requestAnimFrame = function (o) {
    requestAnimFrame(o);
}

/**
* Provides requestAnimationFrame in a cross browser way.
*/
requestAnimFrame = (function () {
    return window.requestAnimationFrame ||
        window.webkitRequestAnimationFrame ||
        window.mozRequestAnimationFrame ||
        window.oRequestAnimationFrame ||
        window.msRequestAnimationFrame ||
        function (/*function FrameRequestCallback*/callback, /*DOMElement Element*/element) {
            window.setTimeout(callback, DRAW_SCENE_INTERVAL);
        };
})();

/**
* WebGL context.
*/
var gl = null;