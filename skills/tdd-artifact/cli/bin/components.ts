#!/usr/bin/env bun
// tdd components: the catalog of what a TDD source can use (templates/components.json).
//   tdd components                 one line per component: name, kind, node type, purpose
//   tdd components <name>          one component in full: syntax, attributes, where it may sit, an example
//   tdd components --json [name]   the catalog (or one entry) as JSON
//   tdd components --gallery <dir> write a packet whose page shows every component beside its markdown, then print
//                                  the two commands that build it
// Exit 0; 1 on an unknown name or a gallery folder that is not empty; 2 on bad usage.
import { describe, listing, loadCatalog, writeGallery } from "../src/components";

const USAGE = "usage: tdd components [<name>] [--json] | tdd components --gallery <dir>";

export async function main(argv: string[]): Promise<number> {
  const args = [...argv];
  const json = args.includes("--json");
  if (json) args.splice(args.indexOf("--json"), 1);
  const g = args.indexOf("--gallery");
  const cat = loadCatalog();
  if (g >= 0) {
    const dir = args[g + 1];
    if (!dir || args.length !== 2) { process.stderr.write(`${USAGE}\n`); return 2; }
    try {
      const files = writeGallery(dir);
      process.stdout.write(`wrote ${files.length} files into ${dir}\nnext:\n  (cd ${dir} && python3 -I "$T/scripts/build_tdd.py" gallery.md build)\n  (cd "$T/app" && bun run build.ts ${dir}/build)\n`);
      return 0;
    } catch (e) {
      process.stderr.write(`tdd components: ${e instanceof Error ? e.message : String(e)}\n`);
      return 1;
    }
  }
  if (args.some((a) => a.startsWith("-")) || args.length > 1) { process.stderr.write(`${USAGE}\n`); return 2; }
  const name = args[0];
  if (!name) {
    process.stdout.write(json ? `${JSON.stringify(cat, null, 1)}\n` : `${listing(cat)}\n`);
    return 0;
  }
  const c = cat.components.find((x) => x.name === name);
  if (!c) {
    process.stderr.write(`tdd components: no component "${name}"; one of: ${cat.components.map((x) => x.name).join(", ")}\n`);
    return 1;
  }
  process.stdout.write(json ? `${JSON.stringify(c, null, 1)}\n` : `${describe(c)}\n`);
  return 0;
}

if (import.meta.main) process.exit(await main(Bun.argv.slice(2)));
