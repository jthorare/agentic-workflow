// Fingerprints of a step's inputs and outputs: sha256 over named parts (files, trees, values). A tree hashes every
// regular file under it by relative path and content, in sorted order, minus the excluded paths; a symlink hashes its
// target string. Used by --reuse (skip a step whose inputs and outputs are unchanged since its last ok) and by the
// resume guard (resume at an earlier step whose inputs changed since it ran).
import { lstatSync, readdirSync, readFileSync, readlinkSync } from "node:fs";
import { join } from "node:path";

export type Part =
  | { readonly file: string }
  | { readonly tree: string; readonly exclude?: readonly string[] }
  | { readonly label: string; readonly value: string };

/** Names that never count, wherever they are: the Finder's folder files, Python's caches, and the page copy
 *  artifact_check writes beside the page while it runs (`.artifact-check-<page>-<pid>.html`). */
const NOISE = new Set([".DS_Store", "__pycache__"]);
const isNoise = (name: string): boolean => NOISE.has(name) || name.startsWith(".artifact-check-");

function fileDigest(p: string): string {
  try {
    const st = lstatSync(p);
    if (st.isSymbolicLink()) return "link:" + readlinkSync(p);
    if (!st.isFile()) return "not-a-file";
    return new Bun.CryptoHasher("sha256").update(readFileSync(p)).digest("hex");
  } catch {
    return "missing";
  }
}

function walk(root: string, rel: string, exclude: readonly string[], out: [string, string][]): void {
  let names: string[];
  try { names = readdirSync(join(root, rel)); } catch { return; }
  for (const name of names.sort()) {
    if (isNoise(name)) continue;
    const r = rel ? `${rel}/${name}` : name;
    if (exclude.some((x) => r === x || r.startsWith(x + "/"))) continue;
    const p = join(root, r);
    let st;
    try { st = lstatSync(p); } catch { continue; }
    if (st.isDirectory()) walk(root, r, exclude, out);
    else out.push([r, fileDigest(p)]);
  }
}

/** sha256 hex over the parts, in the order given. */
export function fingerprint(parts: readonly Part[]): string {
  const h = new Bun.CryptoHasher("sha256");
  for (const part of parts) {
    if ("file" in part) h.update(`file\0${part.file}\0${fileDigest(part.file)}\n`);
    else if ("tree" in part) {
      const rows: [string, string][] = [];
      walk(part.tree, "", part.exclude ?? [], rows);
      h.update(`tree\0${part.tree}\0${rows.length}\n`);
      for (const [r, d] of rows) h.update(`${r}\0${d}\n`);
    } else h.update(`value\0${part.label}\0${part.value}\n`);
  }
  return h.digest("hex");
}
