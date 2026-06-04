import { defineConfig } from "vite"
import react from "@vitejs/plugin-react"
import basicSsl from "@vitejs/plugin-basic-ssl"
import { resolve } from "path"

const isLib = process.env.BUILD_TARGET?.trim() === "lib";

// https://vite.dev/config/
export default defineConfig({
    plugins: [
        react(),
        ...(!isLib ? [basicSsl()] : [])
    ],
    server: {
        host: "0.0.0.0",
        port: 5174,
        https: true as any
    },
    ...(isLib && {
        define: {
            "process.env.NODE_ENV": JSON.stringify("production"),
        },
        build: {
            outDir: "dist",
            lib: {
                entry: resolve(__dirname, "src/main.tsx"),
                name: "AppVR",
                fileName: "app-vr",
                formats: ["iife"],  // IIFE: si autoesegue nel browser, nessun bundler necessario
            },
            rollupOptions: {
                // Non esternalizzare nulla: tutto deve essere incluso nel bundle
                external: [],
            },
            cssCodeSplit: false,    // CSS inline nel JS
        },
    }),
});