class SelectionBuffer {
    constructor(gl) {
        this.gl = gl;
        this.framebuffer = null;
        this.texture = null;
        this.depthBuffer = null;
        this.width = 0;
        this.height = 0;
        this.colorMap = {};
    }

    initialize(width, height) {
        const gl = this.gl;
        this.width = width;
        this.height = height;

        // Create framebuffer
        this.framebuffer = gl.createFramebuffer();
        gl.bindFramebuffer(gl.FRAMEBUFFER, this.framebuffer);

        // Create texture for color attachment
        this.texture = gl.createTexture();
        gl.bindTexture(gl.TEXTURE_2D, this.texture);
        gl.texImage2D(gl.TEXTURE_2D, 0, gl.RGBA, width, height, 0, gl.RGBA, gl.UNSIGNED_BYTE, null);
        gl.texParameteri(gl.TEXTURE_2D, gl.TEXTURE_MIN_FILTER, gl.NEAREST);
        gl.texParameteri(gl.TEXTURE_2D, gl.TEXTURE_MAG_FILTER, gl.NEAREST);
        gl.framebufferTexture2D(gl.FRAMEBUFFER, gl.COLOR_ATTACHMENT0, gl.TEXTURE_2D, this.texture, 0);

        // Create depth buffer
        this.depthBuffer = gl.createRenderbuffer();
        gl.bindRenderbuffer(gl.RENDERBUFFER, this.depthBuffer);
        gl.renderbufferStorage(gl.RENDERBUFFER, gl.DEPTH_COMPONENT16, width, height);
        gl.framebufferRenderbuffer(gl.FRAMEBUFFER, gl.DEPTH_ATTACHMENT, gl.RENDERBUFFER, this.depthBuffer);

        // Check framebuffer status
        const status = gl.checkFramebufferStatus(gl.FRAMEBUFFER);
        if (status !== gl.FRAMEBUFFER_COMPLETE) {
            console.error('Framebuffer not complete:', status);
        }

        // Unbind
        gl.bindTexture(gl.TEXTURE_2D, null);
        gl.bindRenderbuffer(gl.RENDERBUFFER, null);
        gl.bindFramebuffer(gl.FRAMEBUFFER, null);
    }

    buildSelectionColorMap(geometries) {
        if (Object.keys(this.colorMap).length === 0) {
            for (let g = 0; g < geometries.length; g++) {
                let geometry = geometries[g];
                if (!geometry.conceptualFaces) {
                    continue;
                }
                for (let i = 0; i < geometry.instances.length; i++) {
                    let instance = geometry.instances[i];
                    this.colorMap[instance.id] = this.id2rgb(instance.id);
                }
            }
        }
    }

    clearSelectionColorMap() {
        this.colorMap = {};
    }

    id2rgb(id) {
        const STEP = 1.0 / 255.0;

        let R = 0.0;
        let G = 0.0;
        let B = 0.0;

        // R
        if (id >= (255 * 255)) {
            let remainder = Math.floor(id / (255 * 255));
            R = remainder * STEP;

            id -= remainder * (255 * 255);
        }

        // G
        if (id >= 255) {
            let remainder = Math.floor(id / 255);
            G = remainder * STEP;

            id -= remainder * 255;
        }

        // B		
        B = id * STEP;

        return [R, G, B];
    }

    rgb2id(R, G, B) {
        let id = 0;

        // R
        id += R * (255 * 255);

        // G
        id += G * 255;

        // B
        id += B;

        return id;
    }

    bind() {
        this.gl.bindFramebuffer(this.gl.FRAMEBUFFER, this.framebuffer);
        this.gl.viewport(0, 0, this.width, this.height);
    }

    unbind() {
        this.gl.bindFramebuffer(this.gl.FRAMEBUFFER, null);
    }

    readPixel(x, y) {
        const gl = this.gl;
        const pixels = new Uint8Array(4);
        gl.bindFramebuffer(gl.FRAMEBUFFER, this.framebuffer);
        gl.readPixels(x, y, 1, 1, gl.RGBA, gl.UNSIGNED_BYTE, pixels);
        gl.bindFramebuffer(gl.FRAMEBUFFER, null);
        return pixels;
    }

    getInstanceAt(x, y) {
        const pixels = this.readPixel(x, y);
        if (pixels[3] !== 0.0) {
            const instanceId = this.rgb2id(
                pixels[0/*R*/],
                pixels[1/*G*/],
                pixels[2/*B*/]
            );

            return instanceId;
        }

        return -1;
    }

    dispose() {
        const gl = this.gl;
        if (this.texture) gl.deleteTexture(this.texture);
        if (this.depthBuffer) gl.deleteRenderbuffer(this.depthBuffer);
        if (this.framebuffer) gl.deleteFramebuffer(this.framebuffer);
    }
}