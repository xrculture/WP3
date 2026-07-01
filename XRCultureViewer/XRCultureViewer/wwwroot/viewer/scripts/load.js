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

function createTexture_WASM_FS(textureFile, flipY) {
    try {
        var viewer = this;
        var texture = gl.createTexture();

        // Temp texture until the image is loaded
        gl.bindTexture(gl.TEXTURE_2D, texture);
        gl.texImage2D(
            gl.TEXTURE_2D,
            0,
            gl.RGBA,
            1,
            1,
            0,
            gl.RGBA,
            gl.UNSIGNED_BYTE,
            new Uint8Array([0, 0, 0, 255]));

        // Read the file from the WASM file system (synchronous)
        var fileData;
        try {
            fileData = Module.FS.readFile(textureFile, { encoding: 'binary' });
        } catch (err) {
            console.error("Can't load '" + textureFile + "' from WASM file system:", err);
            gl.deleteTexture(texture);
            return null;
        }

        // Convert the binary data to a blob
        var mimeType = getMimeType(textureFile);
        var blob = new Blob([fileData], { type: mimeType });
        var blobUrl = URL.createObjectURL(blob);

        var image = new Image();
        image.addEventListener('error', function () {
            console.error("Can't load image from blob URL.");
            URL.revokeObjectURL(blobUrl); // Clean up
        });

        image.addEventListener('load', function () {
            gl.pixelStorei(gl.UNPACK_FLIP_Y_WEBGL, flipY);
            gl.bindTexture(gl.TEXTURE_2D, texture);
            gl.texImage2D(
                gl.TEXTURE_2D,
                0,
                gl.RGBA,
                gl.RGBA,
                gl.UNSIGNED_BYTE,
                image);

            if (viewer.isPowerOf2(image.width) && viewer.isPowerOf2(image.height)) {
                gl.generateMipmap(gl.TEXTURE_2D);
                gl.texParameteri(gl.TEXTURE_2D, gl.TEXTURE_MIN_FILTER, gl.LINEAR_MIPMAP_LINEAR);
                gl.texParameteri(gl.TEXTURE_2D, gl.TEXTURE_MAG_FILTER, gl.LINEAR);
                gl.texParameteri(gl.TEXTURE_2D, gl.TEXTURE_WRAP_S, gl.REPEAT);
                gl.texParameteri(gl.TEXTURE_2D, gl.TEXTURE_WRAP_T, gl.REPEAT);
            } else {
                gl.texParameteri(gl.TEXTURE_2D, gl.TEXTURE_WRAP_S, gl.CLAMP_TO_EDGE);
                gl.texParameteri(gl.TEXTURE_2D, gl.TEXTURE_WRAP_T, gl.CLAMP_TO_EDGE);
                gl.texParameteri(gl.TEXTURE_2D, gl.TEXTURE_MIN_FILTER, gl.LINEAR);
            }

            gl.bindTexture(gl.TEXTURE_2D, null);
            URL.revokeObjectURL(blobUrl); // Clean up

            PENDING_DRAW_SCENE = true;
        });

        image.src = blobUrl;
        return texture;
    } catch (ex) {
        console.error(ex);
        gl.deleteTexture(texture);
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