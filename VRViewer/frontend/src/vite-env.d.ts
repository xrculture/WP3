/// <reference types="vite/client" />

// Dichiarazioni per importare file .glb e .gltf come URL
declare module '*.glb' {
  const src: string;
  export default src;
}

declare module '*.gltf' {
  const src: string;
  export default src;
}

// Importazione esplicita come URL (es: import model from './model.glb?url')
declare module '*.glb?url' {
  const src: string;
  export default src;
}

declare module '*.gltf?url' {
  const src: string;
  export default src;
}
