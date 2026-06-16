import express, { Request, Response } from "express";
import cors from "cors";
import path from "path";
import fs from "fs";
import https from "https";
import { randomUUID } from "crypto";
import { parseStringPromise, Builder } from "xml2js";

const app = express();
const port = process.env.PORT || 8080;

// Directory where uploaded/downloaded models are stored and served statically
const modelsDir = path.join(__dirname, "..", "public", "models");

// Ensure models directory exists
if (!fs.existsSync(modelsDir)) {
    fs.mkdirSync(modelsDir, { recursive: true });
}

// Middleware
app.use(cors());
app.use(express.json({ limit: "200mb" }));
app.use(express.text({ type: "application/xml", limit: "200mb" }));
app.use(express.text({ type: "text/xml", limit: "200mb" }));

// Serve the frontend web-component (static files from /public, including /public/models)
const publicDir = path.join(__dirname, "..", "public");
app.use(express.static(publicDir));

// ─── Helper: extract a SceneInit value if default="False" ────────────────────
// xml2js with explicitArray:false parses <Foo default="False">val</Foo> as:
//   { _: "val", $: { default: "False" } }  when the node has both text and attrs
// Plain text nodes without attrs are just strings.

type ParsedSceneNode = { _?: string; $?: { default?: string } } | string | undefined;

// Extract string text from a potentially wrapped xml2js node
function str(node: unknown): string {
    if (!node) return "";
    if (typeof node === "string") return node;
    if (typeof node === "object" && "_" in (node as object)) return (node as { _: string })._ ?? "";
    return String(node);
}

function sceneValue(node: ParsedSceneNode): string | undefined {
    if (!node) return undefined;
    if (typeof node === "string") return node || undefined;
    // Has attributes: only use value if default="False"
    if (node.$ && node.$.default?.toLowerCase() === "false" && node._) {
        return node._;
    }
    return undefined;
}

// ─── Helper: build ModelLoadingResponse XML ──────────────────────────────────

function buildXMLResponse(status: number, token: string, message: string, endpoint?: string, loadedContent?: number): string {
    const builder = new Builder({
        headless: false,
        xmldec: { version: "1.0", encoding: "UTF-8" },
        // Preserve URL query separators in XML payloads by using CDATA when escaping would occur.
        cdata: true,
    });
    return builder.buildObject({
        ModelLoadingResponse: {
            Status: status,
            SessionToken: token,
            Message: message,
            ...(loadedContent !== undefined && { LoadedContent: loadedContent }),
            ...(endpoint && { Endpoint: endpoint }),
        }
    });
}

// ─── Helper: build JSON response ─────────────────────────────────────────────

function buildJsonResponse(status: number, token: string, message: string, endpoint?: string, loadedContent?: number): object {
    return {
        Status: status,
        SessionToken: token,
        Message: message,
        ...(loadedContent !== undefined && { LoadedContent: loadedContent }),
        ...(endpoint && { Endpoint: endpoint }),
    };
}

// ─── POST /api/viewer/load ────────────────────────────────────────────────────
// Processes a ModelLoadingRequest (XML or JSON) and returns a ModelLoadingResponse
// in the same format (XML→XML, JSON→JSON).

app.post("/api/viewer/load", async (req: Request, res: Response) => {
    const contentType = req.get("Content-Type") || "";
    const isJson = contentType.includes("application/json");
    const isXml = contentType.includes("xml");

    // Helper to send response in the correct format
    const sendResponse = (statusCode: number, status: number, token: string, message: string, endpoint?: string, loadedContent?: number) => {
        if (isJson) {
            res.status(statusCode).json(buildJsonResponse(status, token, message, endpoint, loadedContent));
        } else {
            res.set("Content-Type", "application/xml");
            res.status(statusCode).send(buildXMLResponse(status, token, message, endpoint, loadedContent));
        }
    };

    let sessionToken = "";
    let source: Record<string, unknown> = {};
    let sceneInit: Record<string, ParsedSceneNode> = {};

    if (isJson) {
        // ── JSON input ────────────────────────────────────────────────────────
        const body = req.body as Record<string, unknown>;
        if (!body || typeof body !== "object") {
            sendResponse(400, 400, "", "Request body must be valid JSON");
            return;
        }
        sessionToken = (body.SessionToken as string) || "";
        source = (body.Source ?? {}) as Record<string, unknown>;
        sceneInit = (body.SceneInit ?? {}) as Record<string, ParsedSceneNode>;
    } else if (isXml) {
        // ── XML input ─────────────────────────────────────────────────────────
        const rawXml = typeof req.body === "string" ? req.body : null;
        if (!rawXml) {
            sendResponse(400, 400, "", "Request body must be raw XML (Content-Type: application/xml)");
            return;
        }

        let parsed: Record<string, unknown>;
        try {
            parsed = await parseStringPromise(rawXml, { explicitArray: false, explicitCharkey: true });
        } catch {
            sendResponse(400, 400, "", "Invalid XML format");
            return;
        }

        const req_ = (parsed.ModelLoadingRequest ?? {}) as Record<string, unknown>;
        sessionToken = str(req_.SessionToken);
        source = (req_.Source ?? {}) as Record<string, unknown>;
        sceneInit = (req_.SceneInit ?? {}) as Record<string, ParsedSceneNode>;
    } else {
        res.status(400).json({ error: "Content-Type must be application/json or application/xml" });
        return;
    }

    // Extract SceneInit parameters
    let bg: string | undefined;
    let zoom: string | undefined;
    let pan: string | undefined;
    let lights: string | undefined;

    if (isJson) {
        // For JSON, SceneInit values are plain strings
        const si = sceneInit as Record<string, unknown>;
        bg = si.BackgroundColor as string | undefined;
        zoom = si.Zoom as string | undefined;
        pan = si.Pan as string | undefined;
        lights = si.Lights as string | undefined;
    } else {
        bg = sceneValue(sceneInit.BackgroundColor);
        zoom = sceneValue(sceneInit.Zoom);
        pan = sceneValue(sceneInit.Pan);
        lights = sceneValue(sceneInit.Lights);
    }

    // Build query string from SceneInit
    const params = new URLSearchParams();
    if (bg)     params.set("bg", bg);
    if (zoom)   params.set("zoom", zoom);
    if (pan)    params.set("pan", pan);
    if (lights) params.set("lights", lights);

    // Determine base URL for the endpoint (use request host)
    const proto = req.protocol;
    const host  = req.get("host") ?? `localhost:${port}`;
    const baseUrl = `${proto}://${host}`;

    // ── Case 1: UrlSource ─────────────────────────────────────────────────────
    const urlSource = (source.UrlSource ?? {}) as Record<string, unknown>;
    const modelUrl = isJson ? (urlSource.Url as string || "") : str(urlSource.Url);
    if (modelUrl) {
        params.set("path", modelUrl);
        const endpoint = `${baseUrl}?${params.toString()}`;
        sendResponse(200, 200, sessionToken, "Ready", endpoint);
        return;
    }

    // ── Case 2: LocalSource (Base64) ─────────────────────────────────────────
    const localSource = (source.LocalSource ?? {}) as Record<string, unknown>;
    const base64 = isJson ? ((localSource.FileContent as string) || "").trim() : str(localSource.FileContent).trim();
    const fileExtRaw = isJson ? ((localSource.FileExtension as string) || ".glb") : (str(localSource.FileExtension) || ".glb");
    const ext = fileExtRaw.startsWith(".") ? fileExtRaw : `.${fileExtRaw}`;

    if (base64) {
        const uuid = randomUUID();
        const modelFile = `${uuid}${ext}`;
        const modelPath = path.join(modelsDir, modelFile);

        try {
            const buffer = Buffer.from(base64, "base64");
            fs.writeFileSync(modelPath, buffer);
        } catch {
            sendResponse(500, 500, sessionToken, "Failed to decode Base64 content");
            return;
        }

        params.set("path", `/models/${modelFile}`);
        const endpoint = `${baseUrl}?${params.toString()}`;
        sendResponse(200, 200, sessionToken, "Ready", endpoint, base64.length * 0.75);
        return;
    }

    sendResponse(400, 400, sessionToken, "No valid Source found (UrlSource.Url or LocalSource.FileContent required)");
});


// ─── Health check ─────────────────────────────────────────────────────────────

app.get("/api/health", (_req: Request, res: Response) => {
    res.json({ status: "ok", timestamp: new Date().toISOString() });
});

// ─── Fallback: serve index.html for SPA routing ───────────────────────────────
// Express 5 requires named wildcard parameter (*splat) instead of bare *

app.get("*splat", (req: Request, res: Response) => {
    // Don't serve index.html for API routes
    if (req.path.startsWith("/api/")) {
        res.status(404).json({ error: "Not found" });
        return;
    }

    const indexPath = path.join(publicDir, "index.html");
    if (fs.existsSync(indexPath)) {
        res.sendFile(indexPath);
    } else {
        res.status(404).send("Web component not found. Run the frontend build first.");
    }
});

// ─── Start server ─────────────────────────────────────────────────────────────

const keyPath  = process.env.SSL_KEY  ?? "/app/key.pem";
const certPath = process.env.SSL_CERT ?? "/app/cert.pem";

if (fs.existsSync(keyPath) && fs.existsSync(certPath)) {
    https.createServer({ key: fs.readFileSync(keyPath), cert: fs.readFileSync(certPath) }, app)
        .listen(port, () => {
            console.log(`XR-Culture server running on https://localhost:${port}`);
            console.log(`  Static files: ${publicDir}`);
        });
} else {
    app.listen(port, () => {
        console.log(`XR-Culture server running on http://localhost:${port}`);
        console.log(`  Static files: ${publicDir}`);
    });
}