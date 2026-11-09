// PR 10 acceptance, compile half: parity/oracle.py sd_calls compiles each packet in $TDD_PARITY_PACKETS with
// build_tdd.py (system-design pinned to the vendor copy) and records every call the compiler makes to the D1 surface:
// arguments, result, and for the Inline / Assets objects their state after each call. This replays the calls, in
// order, through src/sd (with bd.MARK set to build_tdd's pattern, as the compiler sets it) and requires every result
// and state to be equal. It runs twice per packet: --no-copy (the compile the design measured) and with copying, where
// Assets.finish copies into a folder of the test's own (the oracle's folder path is mapped onto it). Then every image
// under the packet goes through img_size and through Assets(inline=True).ref(embed=True).
import { describe, expect, test } from "bun:test";
import { existsSync, mkdtempSync, rmSync, statSync } from "node:fs";
import { tmpdir } from "node:os";
import { basename, join } from "node:path";
import * as py from "../../src/py";
import { bd, bp, type Assets, type Inline } from "../../src/sd";
import { SKIP_REASON, UPSTREAM, attempt, fromTag, hasPython, runOracle, same, show, tagOf, uriDigest, type Outcome, type Tagged } from "./harness";

const PACKETS = (process.env.TDD_PARITY_PACKETS ?? "").split(":").filter(Boolean);

interface Call { f: string; a: Tagged[]; k: Record<string, Tagged>; o?: number; r?: Tagged; x?: [string, string]; s?: Tagged }
interface Golden {
  sha256: Record<string, string>; root: string; packet: string; src: string; out: string; copy: boolean;
  mark: [string, number]; stdout: string; stderr: string; calls: Call[]; images: [string, Outcome, Outcome][];
}

/** Map the oracle's (deleted) out folder onto ours, in every string of a tagged tree. */
function remap(t: Tagged, from: string, to: string): Tagged {
  const s = (v: string) => (v === from ? to : v.startsWith(from + "/") ? to + v.slice(from.length) : v);
  switch (t.t) {
    case "s": return { t: "s", v: s(t.v) };
    case "l": return { t: "l", v: t.v.map((x) => remap(x, from, to)) };
    case "d": return { t: "d", v: t.v.map(([k, x]) => [s(k), remap(x, from, to)] as [string, Tagged]) };
    default: return t;
  }
}

const assetsState = (a: Assets) => ({ out_dir: a.out_dir, inline: a.inline, copy: a.copy, files: a.files, missing: a.missing, order: a.order });

/** One recorded call through src/sd: {r, s?} or {x}. */
function replay(c: Call, args: any[], kw: Record<string, any>, objs: Map<number, unknown>): { r?: Tagged; s?: Tagged; x?: unknown } {
  const arg = (i: number, name: string, dflt: unknown) => (name in kw ? kw[name] : i < args.length ? args[i] : dflt);
  const run = (fn: () => unknown, state?: (r: any) => unknown) => {
    const o = attempt(fn);
    if ("x" in o) return { x: o.x };
    return state ? { r: o.r, s: tagOf(state(null)) } : { r: o.r };
  };
  switch (c.f) {
    case "bd.parse_blocks": return run(() => bd.parse_blocks(args[0]));
    case "bd.split_marker": return run(() => bd.split_marker(args[0]));
    case "bd.slug": return run(() => bd.slug(args[0]));
    case "bd.strip_tags": return run(() => bd.strip_tags(args[0]));
    case "bd.img_size": return run(() => bd.img_size(args[0]));
    case "bd.published_for": return run(() => bd.published_for(args[0], args[1]));
    case "bd.page_title": return run(() => bd.page_title(args[0]));
    case "bd.load_json": return run(() => bd.load_json(args[0], args[1]));
    case "bd.esc": return run(() => bd.esc(args[0], arg(1, "quote", true)));
    case "bd.FILE_LIMIT": return run(() => bd.FILE_LIMIT);
    case "bd.STATUS_WORDS.get": return run(() => bd.STATUS_WORDS.get(args[0]) ?? (args.length > 1 ? args[1] : null));
    case "bd.Inline": {
      const inl = bd.Inline();
      objs.set(c.o!, inl);
      return run(() => null, () => inl.anchor_map);
    }
    case "bd.Inline.__call__": {
      const inl = objs.get(c.o!) as Inline;
      return run(() => inl(args[0], arg(1, "links", true)), () => inl.anchor_map);
    }
    case "bd.Assets": {
      const a = new bd.Assets(arg(0, "out_dir", null), arg(1, "inline", null), arg(2, "copy", null));
      objs.set(c.o!, a);
      return run(() => null, () => assetsState(a));
    }
    case "bd.Assets.ref": {
      const a = objs.get(c.o!) as Assets;
      return run(() => a.ref(arg(0, "published", null), arg(1, "candidates", null), arg(2, "embed", false)), () => assetsState(a));
    }
    case "bd.Assets.finish": {
      const a = objs.get(c.o!) as Assets;
      return run(() => a.finish(), () => assetsState(a));
    }
    case "bp.front": return run(() => bp.front(args[0]));
    case "bp.first_sentence": return run(() => bp.first_sentence(args[0]));
    case "bp.inline": return run(() => bp.inline(args[0]));
    case "bp.markdown": return run(() => bp.markdown(args[0]));
    default: throw new Error(`the oracle recorded a call to ${c.f}, which src/sd does not replay`);
  }
}

/** The kernel value of a tagged argument (str, list, bool, None and int are all the compile passes). */
const arg = (t: Tagged): any => fromTag(t);

describe.skipIf(!hasPython || !PACKETS.length)("a compile's calls to system-design, replayed through src/sd", () => {
  if (!hasPython) console.warn(SKIP_REASON);
  for (const packet of PACKETS) {
    for (const copy of [false, true]) {
      test(`${basename(packet)} (${copy ? "copying" : "--no-copy"}): every call, result and object state`, () => {
        const g = runOracle<Golden>("sd_calls", ["--packet", packet, ...(copy ? ["--copy"] : [])]);
        // the oracle ran the pinned system-design
        expect(g.sha256).toEqual(Object.fromEntries(Object.entries(UPSTREAM.files).map(([f, p]) => [f, p.sha256])));
        expect(g.stdout).toMatch(/: \d+ sections, \d+ diagrams, .* files \(\d+ missing\)\n$/);
        const out = mkdtempSync(join(tmpdir(), "tdd-sd-replay-"));
        const prev = bd.MARK;
        const cwd = process.cwd();
        const diffs: string[] = [];
        const names = new Map<string, number>();
        try {
          bd.MARK = py.re.compile(g.mark[0], g.mark[1]);
          process.chdir(g.packet);
          const objs = new Map<number, unknown>();
          g.calls.forEach((c, k) => {
            const args = c.a.map((t) => arg(remap(t, g.out, out)));
            const kw = Object.fromEntries(Object.entries(c.k).map(([n, t]) => [n, arg(remap(t, g.out, out))]));
            const want: Record<string, unknown> = "x" in c ? { x: c.x } : { r: remap(c.r!, g.out, out), ...(c.s ? { s: remap(c.s, g.out, out) } : {}) };
            const got = replay(c, args, kw, objs);
            names.set(c.f, (names.get(c.f) ?? 0) + 1);
            if (!same(want, got)) diffs.push(`#${k} ${c.f}(${show(c.a, 120)}): python ${show(want)} ts ${show(got)}`);
          });
          if (copy) {
            const fin = g.calls.find((c) => c.f === "bd.Assets.finish")!;
            const files = fromTag(remap(fin.r!, g.out, out)) as Map<string, string>;
            for (const [, dest] of files) if (dest.startsWith(out)) expect(existsSync(dest) && statSync(dest).isFile()).toBe(true);
          }
          // every image file under the packet: img_size, and the data: URL branch of Assets.ref
          for (const [p, size, emb] of g.images) {
            const a = new bd.Assets(out, true, false);
            const gotSize = attempt(() => bd.img_size(p));
            const gotEmb = attempt(() => uriDigest(a.ref("x/" + basename(p), [p], true)));
            if (!same(size, gotSize)) diffs.push(`img_size(${p}): python ${show(size)} ts ${show(gotSize)}`);
            if (!same(emb, gotEmb)) diffs.push(`embed(${p}): python ${show(emb)} ts ${show(gotEmb)}`);
          }
        } finally {
          process.chdir(cwd);
          bd.MARK = prev;
          rmSync(out, { recursive: true, force: true });
        }
        const summary = [...names].map(([n, c]) => `${n} ${c}`).join(", ");
        console.log(`${basename(packet)}${copy ? " (copying)" : ""}: ${g.calls.length} calls replayed, ${g.images.length} images, ${diffs.length} diffs; ${summary}`);
        expect(diffs.slice(0, 8)).toEqual([]);
        expect(g.calls.length).toBeGreaterThan(1000);
        for (const n of ["bd.parse_blocks", "bd.Inline.__call__", "bd.strip_tags", "bd.slug", "bd.esc", "bd.img_size", "bd.Assets.ref",
          "bd.Assets.finish", "bd.published_for", "bd.load_json", "bd.split_marker", "bp.front", "bp.markdown", "bp.inline"]) expect(names.get(n) ?? 0).toBeGreaterThan(0);
      }, 120_000);
    }
  }
});
