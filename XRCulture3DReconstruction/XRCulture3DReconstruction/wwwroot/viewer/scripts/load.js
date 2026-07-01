var g_onRuntimeInitializedEvent = null;

var Module = {
    onRuntimeInitialized: function () {
        window.logInfo("[onRuntimeInitialized] Runtime initialized successfully.");

        // Custom event handler
        if (g_onRuntimeInitializedEvent !== null) {
            g_onRuntimeInitializedEvent();
        }
    },
}

Module.onAbort = function (what) {
    console.error('Module aborted:', what);
};

Module.onRuntimeError = function (err) {
    console.error('Runtime error:', err);
};

function jsLogCallback(msg) {
    console.log(msg);
}

function getFileExtension(file) {
    if (file && file.includes('.')) {
        const parts = file.split('.');
        if (parts.length > 1 && parts[parts.length - 1].length > 0) {
            return parts[parts.length - 1];
        }
    }

    return null;
}

function getFileNameWithExtension(file) {
    if (!file) {
        return null;
    }

    // Extract basename (handle both forward and backslashes)
    return file.split(/[\\/]/).pop();
}

function getFileNameWithoutExtension(file) {
    if (!file) {
        return null;
    }

    // Extract basename (handle both forward and backslashes)
    const basename = file.split(/[\\/]/).pop();

    if (basename.includes('.')) {
        const parts = basename.split('.');
        if (parts.length > 1) {
            parts.pop(); // Remove extension
            return parts.join('.');
        }
    }

    return basename;
}

function getFileNameWithoutExtension2(file) {
    if (file && file.includes('.')) {
        const parts = file.split('.');
        if (parts.length > 1) {
            // Remove the last part (extension) and rejoin
            parts.pop();
            return parts.join('.');
        }
    }

    // Return the original file if no extension found
    return file || null;
}

function getMimeType(fileName) {
    const ext = getFileExtension(fileName)?.toLowerCase();
    const mimeTypes = {
        'png': 'image/png',
        'jpg': 'image/jpeg',
        'jpeg': 'image/jpeg',
        'bmp': 'image/bmp',
        'gif': 'image/gif',
        'webp': 'image/webp'
    };
    return mimeTypes[ext] || 'application/octet-stream';
}

// Patch for Samsung: resize canvas to power-of-two dimensions to avoid GL errors when uploading textures
function createTexture_WASM_FS(path, flipY) {
    try {
        var viewer = this;

        var texture = gl.createTexture();
        gl.bindTexture(gl.TEXTURE_2D, texture);
        // Grey 1x1 placeholder while decoding
        gl.texImage2D(gl.TEXTURE_2D, 0, gl.RGBA, 1, 1, 0, gl.RGBA, gl.UNSIGNED_BYTE,
            new Uint8Array([180, 180, 180, 255]));
        gl.texParameteri(gl.TEXTURE_2D, gl.TEXTURE_MIN_FILTER, gl.LINEAR);
        gl.texParameteri(gl.TEXTURE_2D, gl.TEXTURE_MAG_FILTER, gl.LINEAR);
        gl.bindTexture(gl.TEXTURE_2D, null);

        var data;
        try {
            data = Module.FS.readFile(path);
        } catch (fsErr) {
            window.logWarn('[createTexture_WASM_FS] File not in WASM FS: ' + path);
            return texture;
        }

        var ext = path.split('.').pop().toLowerCase();
        var mimeType = (ext === 'png') ? 'image/png' : 'image/jpeg';
        var blob = new Blob([data], { type: mimeType });
        var objectUrl = URL.createObjectURL(blob);

        var image = new Image();

        image.onerror = function () {
            window.logErr('[createTexture_WASM_FS] Image decode failed: ' + path);
            URL.revokeObjectURL(objectUrl);
        };

        image.onload = function () {
            try {
                window.logWarn('[createTexture_WASM_FS] texImage2D: source=' +
                    image.width + 'x' + image.height +
                    ', MAX_TEXTURE_SIZE=' + maxSize);

                var maxSize = gl.getParameter(gl.MAX_TEXTURE_SIZE);
                var srcW = image.width;
                var srcH = image.height;

                // Scale down to fit MAX_TEXTURE_SIZE, preserving aspect ratio
                var scale = Math.min(1.0, maxSize / Math.max(srcW, srcH));
                var dstW = Math.floor(srcW * scale);
                var dstH = Math.floor(srcH * scale);
                window.logInfo(`${srcW}, ${srcH}`);

                if (scale < 1.0) {
                    window.logWarn('[createTexture_WASM_FS] Downscaling texture from ' +
                        srcW + 'x' + srcH + ' to ' + dstW + 'x' + dstH +
                        ' (MAX_TEXTURE_SIZE=' + maxSize + '): ' + path);
                }

                // Use canvas to draw (handles both flip and resize)
                var canvas = document.createElement('canvas');
                canvas.width = dstW;
                canvas.height = dstH;
                var ctx = canvas.getContext('2d');

                if (flipY) {
                    ctx.translate(0, dstH);
                    ctx.scale(1, -1);
                }

                ctx.drawImage(image, 0, 0, dstW, dstH);

                gl.bindTexture(gl.TEXTURE_2D, texture);
                gl.pixelStorei(gl.UNPACK_PREMULTIPLY_ALPHA_WEBGL, false);
                gl.pixelStorei(gl.UNPACK_FLIP_Y_WEBGL, false);
                gl.texImage2D(gl.TEXTURE_2D, 0, gl.RGBA, gl.RGBA, gl.UNSIGNED_BYTE, canvas);

                var isPOT = (dstW & (dstW - 1)) === 0 && (dstH & (dstH - 1)) === 0;
                if (isPOT) {
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
                window.logInfo('[createTexture_WASM_FS] Loaded: ' + path +
                    ' (' + dstW + 'x' + dstH + ', flipY=' + flipY + ')');
            } catch (uploadErr) {
                window.logErr('[createTexture_WASM_FS] GL upload failed: ' + uploadErr);
            } finally {
                URL.revokeObjectURL(objectUrl);
                PENDING_DRAW_SCENE = true;
            }
        };

        image.src = objectUrl;
        return texture;

    } catch (e) {
        window.logErr('[createTexture_WASM_FS] ' + e);
    }
    return null;
}

function loadTexture(zip, textureName) {
    if (zip) {
        // Load texture from JSZip
        zip.file(textureName).async('blob').then(function (blob) {
            g_viewer._textures[textureName] = g_viewer.createTextureBLOB(blob, true)
        })
    } else {
        // Load texture from WASM file system
        g_viewer._textures[textureName] = createTexture_WASM_FS.call(g_viewer, '/data/' + textureName, true);
    }
}

function loadTexture2(zip, textureName, flipY) {
    if (zip) {
        // Load texture from JSZip
        zip.file(textureName).async('blob').then(function (blob) {
            g_viewer._textures[textureName] = g_viewer.createTextureBLOB(blob, flipY)
        })
    } else {
        // Load texture from WASM file system
        g_viewer._textures[textureName] = createTexture_WASM_FS.call(g_viewer, '/data/' + textureName, flipY);
    }
}

function readFileByUri(file, callback) {
    try {
        var rawFile = new XMLHttpRequest();
        rawFile.open('GET', encodeURIComponent(file));
        rawFile.responseType = "arraybuffer";
        rawFile.onreadystatechange = function () {
            if (rawFile.readyState === 4 && rawFile.status === 200) {
                callback(new Uint8Array(rawFile.response));
            }
        }
        rawFile.send();
    }
    catch (ex) {
        console.error(ex);
    }
}