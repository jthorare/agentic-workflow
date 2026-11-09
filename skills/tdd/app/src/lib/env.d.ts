// Minimal ambient types for the two bun scripts (build.ts, check_hydrate.ts). The app installs neither @types/bun nor
// @types/jsdom (bun.lock is not the app-core worker's to change), so these declare exactly the calls the scripts make.
// Delete this file if those packages are added to devDependencies.
interface BunFile { text(): Promise<string>; json(): Promise<any>; exists(): Promise<boolean>; readonly size: number }
interface BunBuildArtifact { path: string; kind: string; text(): Promise<string> }
interface BunBuildOutput { success: boolean; logs: unknown[]; outputs: BunBuildArtifact[] }
interface BunBuildOptions {
  entrypoints: string[]; target?: "browser" | "bun" | "node"; format?: "esm" | "cjs" | "iife"; minify?: boolean;
  define?: Record<string, string>; sourcemap?: "none" | "inline" | "external"; tsconfig?: string;
}
declare const Bun: {
  file(path: string): BunFile;
  write(path: string, data: string | Uint8Array): Promise<number>;
  build(options: BunBuildOptions): Promise<BunBuildOutput>;
  gzipSync(data: string | Uint8Array): Uint8Array;
};
declare const process: {
  argv: string[]; env: Record<string, string | undefined>; exitCode?: number;
  exit(code?: number): never; cwd(): string;
};
interface ImportMeta { readonly dir: string; readonly path: string }
declare module "jsdom" {
  export class VirtualConsole {
    on(event: "error" | "warn" | "log" | "info" | "debug", fn: (...args: unknown[]) => void): this;
    on(event: "jsdomError", fn: (err: Error) => void): this;
  }
  export interface ConstructorOptions {
    url?: string; runScripts?: "dangerously" | "outside-only"; pretendToBeVisual?: boolean; virtualConsole?: VirtualConsole;
    resources?: "usable"; beforeParse?: (window: Window & typeof globalThis) => void;
  }
  export class JSDOM {
    constructor(html?: string, options?: ConstructorOptions);
    readonly window: Window & typeof globalThis;
    serialize(): string;
  }
}
