// FILES: the publish manifest (nothing missing, nothing over 15 MB, the page under 16 MB), no orphan media, every
// prs/pr-NN.md loaded onto the page, and no scaffold placeholder left (lint_packet.Lint.r_files and .placeholders).
import { Effect } from "effect";
import * as py from "../../py";
import { formatFixed } from "../../py/num";
import { Packet, type LintPacket } from "../packet";
import { COMMENT, FILE_LIMIT, MEDIA_EXT, PAGE_LIMIT, PLACEHOLDER, PLACEHOLDER_DATE, PLACEHOLDER_MARK } from "../patterns";
import { type PyValue, dget, get, isDict, isOSError, isOsOrValueError, items, iter, or, str, truthy } from "../pyops";

const re = py.re;
const { join, basename, relpath } = py.path;
const PR_FILE = re.compile(String.raw`pr-\d+[a-z]?\.md`);
const mb = (n: number): string => formatFixed(n / 1048576, 1);
const MARK_BYTES = Buffer.from(PLACEHOLDER_MARK, "utf8");

/** data.count(b"\n", 0, end) */
function newlinesBefore(data: Uint8Array, end: number): number {
  let n = 0;
  for (let i = 0; i < end; i++) if (data[i] === 0x0a) n++;
  return n;
}

/**
 * lint_packet.Lint.placeholders: FILES/placeholder (a file the page publishes, from the manifest's files, still carries
 * the scaffold's marker) and FILES/placeholder-text (a line of the source or of status.md, one-line comments aside,
 * still carries a scaffold placeholder). The text check needs no manifest.
 */
function placeholders(p: LintPacket, man: PyValue | null): void {
  for (const [pub, src] of man !== null ? items(or(dget(man, "files"), new Map())) : []) {
    if (!truthy(src) || !p.fs.isfile(str(src))) continue;
    let data: Uint8Array;
    try {
      data = p.fs.readBytes(str(src));
    } catch (e) {
      if (!isOSError(e)) throw e;
      continue;
    }
    const at = Buffer.from(data.buffer, data.byteOffset, data.byteLength).indexOf(MARK_BYTES);
    if (at >= 0) {
      // the build writes resolved paths: name the file relative to the packet even when its path goes through a link
      const real = p.fs.realpath(str(src));
      const root = p.fs.realpath(p.root);
      const where = real.startsWith(root + "/") ? relpath(real, root) : p.rel(str(src));
      p.add("FILES", "placeholder", "error", `${pub} is still the scaffold's placeholder image (it says "${PLACEHOLDER_MARK}"): replace it with the real diagram or shot before publishing`,
        where, newlinesBefore(data, at) + 1);
    }
  }
  for (const path of [...(p.src ? [p.src] : []), join(p.root, "status.md")]) {
    if (!p.fs.isfile(path)) continue;
    const lines = p.lines(path);
    for (let i = 0; i < lines.length; i++) {
      const line = lines[i]!;
      if (COMMENT.match(line)) continue;
      const found: string[] = [];
      for (const m of PLACEHOLDER.finditer(line)) {
        const t = m.group(0)!;
        if (!found.includes(t)) found.push(t);
      }
      if (found.length) {
        const names = found.map((t) => `"${t}"`).join(", ");
        p.add("FILES", "placeholder-text", "error", `scaffold placeholder left in the text: ${names}; write the real text (or delete the line) before publishing. ` +
          `The scaffold's placeholders are the "${PLACEHOLDER_MARK}" marker, the ${PLACEHOLDER_DATE} date and the template's <angle-bracket> prompts`, p.rel(path), i + 1);
      }
    }
  }
}

export const files = Effect.gen(function* () {
  const p = yield* Packet;
  const man = p.manifest;
  placeholders(p, man);
  if (man === null) return;
  const mrel = !p.compiled ? "build/content.files.json" : "(compiled) content.files.json";
  const filesMap = or(dget(man, "files"), new Map());
  for (const m of iter(or(dget(man, "missing"), []))) {
    const ms = str(m);
    const [f, l] = p.locate(ms);
    p.add("FILES", "missing", "error", `${ms} is referenced by the page but not found`, f || mrel, l);
  }
  for (const [pub, src] of items(filesMap)) {
    if (!truthy(src) || !p.fs.isfile(str(src))) {
      p.add("FILES", "missing", "error", `${pub}: manifest source ${str(p.rel(src))} does not exist`, mrel);
    } else if (p.fs.getsize(str(src)) > FILE_LIMIT) {
      p.add("FILES", "too-big", "error", `${pub} is ${mb(p.fs.getsize(str(src)))} MB; the artifact limit per file is 15 MB`, mrel);
    }
  }
  for (const x of iter(or(dget(man, "over_15mb"), []))) {
    if (!(typeof x === "string" && filesMap instanceof Map && filesMap.has(x))) p.add("FILES", "too-big", "error", `${str(x)} is over 15 MB`, mrel);
  }
  if (!p.compiled) {
    const page = join(p.build, str(or(dget(man, "page"), "tdd.html")));
    if (p.fs.isfile(page)) {
      const size = p.fs.getsize(page);
      if (size > PAGE_LIMIT) p.add("FILES", "page-size", "error", `${p.rel(page)} is ${mb(size)} MB; the page limit is 16 MB`, p.rel(page));
    } else {
      p.note(`FILES: ${p.rel(page)} is not built yet (app/build.ts writes it); page size not checked`);
    }
    const pb = dget(man, "page_bytes");
    const pbn = typeof pb === "number" ? pb : typeof pb === "bigint" ? Number(pb) : pb instanceof py.PyFloat ? pb.value : typeof pb === "boolean" ? (pb ? 1 : 0) : null;
    if (pbn !== null && pbn > PAGE_LIMIT) p.add("FILES", "page-size", "error", `page_bytes ${str(pb)} is over 16 MB`, mrel);
  }
  // orphan media: published by no manifest of any page in the packet, and shown by no hotspot
  const used = new Set<string>();
  const manifests: PyValue[] = [man];
  for (const f of p.fs.listdir(p.root)) if (f.endsWith(".files.json")) manifests.push(p.load_json(join(p.root, f), "FILES"));
  for (const m of manifests) {
    for (const [pub, src] of items(or(dget(or(m, new Map()), "files"), new Map()))) {
      used.add(p.fs.realpath(join(p.root, pub)));
      if (truthy(src)) used.add(p.fs.realpath(str(src)));
    }
  }
  const ddir = join(p.root, "diagrams");
  if (p.fs.isdir(ddir)) {
    for (const f of p.fs.listdir(ddir)) {
      if (!f.endsWith(".hotspots.json")) continue;
      let spec: PyValue;
      try {
        spec = py.json.loads(p.fs.readText(join(ddir, f)));
      } catch (e) {
        if (!isOsOrValueError(e)) throw e;
        continue;
      }
      // outside the Python's try only in form: its handler catches ValueError and OSError, which these lines never raise
      for (const b of iter(dget(spec, "boxes", []))) if (truthy(dget(b, "image"))) used.add(p.fs.realpath(join(p.root, str(dget(b, "image")))));
    }
  }
  for (const sub of ["mockups", "diagrams"]) {
    const d = join(p.root, sub);
    if (!p.fs.isdir(d)) continue;
    for (const [dirpath, , fns] of p.fs.walk(d)) {
      for (const fn of py.sorted(fns)) {
        const path = join(dirpath, fn);
        if (!MEDIA_EXT.some((e) => py.lower(fn).endsWith(e)) || used.has(p.fs.realpath(path))) continue;
        const rp = p.rel(path)!;
        const [f, l] = p.locate(rp);
        const why = f ? `; it is written at ${f}:${str(l)} but the page did not pick it up` : "";
        p.add("FILES", "orphan", "warn", `${rp} is in the packet but on no page${why}`, rp);
        if (p.fs.getsize(path) > FILE_LIMIT) p.add("FILES", "too-big", "error", `${rp} is over 15 MB`, rp);
      }
    }
  }
  const loaded = new Set<string>();
  const prsOf = (node: PyValue): void => {
    if (isDict(node)) {
      if (get(node, "type") === "prs") {
        for (const g of iter(get(node, "groups", []))) {
          for (const q of iter(get(g, "prs", []))) if (truthy(get(q, "file"))) loaded.add(basename(str(get(q, "file"))));
        }
      }
      for (const v of node.values()) prsOf(v);
    } else if (Array.isArray(node)) {
      for (const x of node) prsOf(x);
    }
  };
  prsOf(get(or(p.content, new Map()), "sections", []));
  for (const path of p.prs) {
    const fn = basename(path);
    if (!PR_FILE.fullmatch(fn)) {
      p.add("FILES", "pr-unloaded", "warn", `prs/${fn} is not named pr-NN.md or pr-NNa.md (a sub-PR), so the PR loader skips it and it is not on the page`, p.rel(path));
    } else if (loaded.size && !loaded.has(fn)) {
      p.add("FILES", "pr-unloaded", "error", `prs/${fn} is a PR file but the page has no card for it (the manifest skipped it)`, p.rel(path));
    }
  }
});
