// FILES: the publish manifest (nothing missing, nothing over 15 MB, the page under 16 MB), no orphan media, and every
// prs/pr-NN.md loaded onto the page (lint_packet.Lint.r_files).
import { Effect } from "effect";
import * as py from "../../py";
import { formatFixed } from "../../py/num";
import { Packet } from "../packet";
import { FILE_LIMIT, MEDIA_EXT, PAGE_LIMIT } from "../patterns";
import { type PyValue, get, isDict, items, iter, or, str, truthy } from "../pyops";

const re = py.re;
const { join, basename } = py.path;
const PR_FILE = re.compile(String.raw`pr-\d+[a-z]?\.md`);
const mb = (n: number): string => formatFixed(n / 1048576, 1);

export const files = Effect.gen(function* () {
  const p = yield* Packet;
  const man = p.manifest;
  if (man === null) return;
  const mrel = !p.compiled ? "build/content.files.json" : "(compiled) content.files.json";
  const filesMap = or(get(man, "files"), new Map());
  for (const m of iter(or(get(man, "missing"), []))) {
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
  for (const x of iter(or(get(man, "over_15mb"), []))) {
    if (!(typeof x === "string" && filesMap instanceof Map && filesMap.has(x))) p.add("FILES", "too-big", "error", `${str(x)} is over 15 MB`, mrel);
  }
  if (!p.compiled) {
    const page = join(p.build, str(or(get(man, "page"), "tdd.html")));
    if (p.fs.isfile(page)) {
      const size = p.fs.getsize(page);
      if (size > PAGE_LIMIT) p.add("FILES", "page-size", "error", `${p.rel(page)} is ${mb(size)} MB; the page limit is 16 MB`, p.rel(page));
    } else {
      p.note(`FILES: ${p.rel(page)} is not built yet (app/build.ts writes it); page size not checked`);
    }
    const pb = get(man, "page_bytes");
    const pbn = typeof pb === "number" ? pb : typeof pb === "bigint" ? Number(pb) : pb instanceof py.PyFloat ? pb.value : typeof pb === "boolean" ? (pb ? 1 : 0) : null;
    if (pbn !== null && pbn > PAGE_LIMIT) p.add("FILES", "page-size", "error", `page_bytes ${str(pb)} is over 16 MB`, mrel);
  }
  // orphan media: published by no manifest of any page in the packet, and shown by no hotspot
  const used = new Set<string>();
  const manifests: PyValue[] = [man];
  for (const f of p.fs.listdir(p.root)) if (f.endsWith(".files.json")) manifests.push(p.load_json(join(p.root, f), "FILES"));
  for (const m of manifests) {
    for (const [pub, src] of items(or(get(or(m, new Map()), "files"), new Map()))) {
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
      } catch {
        continue;
      }
      for (const b of iter(get(spec, "boxes", []))) if (truthy(get(b, "image"))) used.add(p.fs.realpath(join(p.root, str(get(b, "image")))));
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
