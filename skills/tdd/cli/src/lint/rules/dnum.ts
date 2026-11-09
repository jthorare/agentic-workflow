// DNUM: decision D-numbers unique and gapless from D1, a status on each, a date on each done one, all in the decisions
// section; a page that cites D-numbers but defines no decision blocks (lint_packet.Lint.r_dnum).
import { Effect } from "effect";
import * as py from "../../py";
import { Packet, type Loc } from "../packet";
import { ANY_DATE, TOKEN } from "../patterns";
import { type PyDict, eq, get, iter, outside_fences, str, strip, truthy } from "../pyops";

const re = py.re;
const DTOK = re.compile(String.raw`(?<![\w#/.\-])(D-?\d+)(?![\w\-]|\.\d)`);
const DNUM = re.compile(String.raw`D-?(\d+)`);
const toInt = (v: number | bigint): bigint => BigInt(v);

export const dnum = Effect.gen(function* () {
  const p = yield* Packet;
  const c = p.content;
  if (!truthy(c)) return;
  const decs = get(c, "decisions", []);
  const s4 = p.slots.length ? p.slots[4]?.[1] ?? null : null;
  const dsec = s4 ? get(s4, "id") : null;
  if (!truthy(decs)) {
    const set = new Set<string>();
    for (const f of p.text_files()) {
      for (const [, l] of outside_fences(p.lines(f))) for (const m of DTOK.finditer(l)) set.add(m.group(1)!);
    }
    // sorted(set, key=int(digits)); Python's ties (D7 and D-7) follow set order, which is hash-seeded: kept in first-seen order here
    const dtoks = [...set]
      .map((t) => [t, toInt(py.int(re.sub(String.raw`\D`, "", t)))] as const)
      .sort((a, b) => (a[1] < b[1] ? -1 : a[1] > b[1] ? 1 : 0))
      .map(([t]) => t);
    if (dtoks.length) {
      p.add("DNUM", "none", "warn", `the page refers to ${dtoks.length} D-numbers (${dtoks.slice(0, 8).join(", ")}${dtoks.length > 8 ? "…" : ""}) but defines ` +
        `no ::: decision blocks; write each ruled decision as a decision box in the decisions section`, p.rel(p.src));
    }
    return;
  }
  const seen = new Map<unknown, number | null>();
  const dnums: [bigint, string | null, number | null][] = [];
  const nth = new Map<unknown, number>();
  for (const d of iter(decs) as PyDict[]) {
    const did = get(d, "id", "");
    const didS = str(did);
    const hits = p.locate_all(re.compile(String.raw`^\s*:{3,}\s*decision\s+` + re.escape(didS) + String.raw`(?![\w.])`), p.src ? [p.src] : null);
    const k = (nth.get(did) ?? -1) + 1;
    nth.set(did, k);
    const [f, l]: Loc = k < hits.length ? hits[k]! : hits.length ? hits[0]! : [null, null];
    if (seen.has(did)) {
      p.add("DNUM", "duplicate", "error", `${didS} is defined twice (first at line ${str(seen.get(did))}); D-numbers are unique per doc`, f, l);
    }
    if (!seen.has(did)) seen.set(did, l);
    const m = typeof did === "string" ? DNUM.fullmatch(did) : null;
    if (m) dnums.push([toInt(py.int(m.group(1)!)), f, l]);
    else if (!(typeof did === "string" && TOKEN.fullmatch(did))) {
      p.add("DNUM", "bad-id", "error", `decision '${didS}' does not start with a reference token (write ::: decision D7 · the question)`, f, l);
    }
    const status = strip(py.truthy(get(d, "status")) ? get(d, "status") : "");
    if (!status) {
      p.add("DNUM", "no-status", "error", `${didS} has no status (add {status: <owner>, YYYY-MM-DD} or {status: Open · ...})`, f, l);
    } else if (get(d, "state") === "done" && !ANY_DATE.search(status)) {
      p.add("DNUM", "done-no-date", "error", `${didS} is decided but its status '${status}' carries no YYYY-MM-DD date`, f, l);
    }
    if (truthy(dsec) && !eq(get(d, "section"), dsec)) {
      p.add("DNUM", "outside-section", "warn", `${didS} sits in section '${str(get(d, "section"))}', not in the decisions section '${str(dsec)}'`, f, l);
    }
  }
  if (dnums.length) {
    const have = new Set(dnums.map(([n]) => n));
    let top = 0n;
    for (const n of have) if (n > top) top = n;
    const gap: bigint[] = [];
    for (let n = 1n; n <= top; n++) if (!have.has(n)) gap.push(n);
    if (gap.length) {
      const [, f, l] = dnums[0]!;
      const defined = [...have].sort((a, b) => (a < b ? -1 : a > b ? 1 : 0));
      p.add("DNUM", "gap", "error", `D-numbers must run from D1 with no gap: missing ${gap.map((n) => `D${n}`).join(", ")} ` +
        `(defined: ${defined.map((n) => `D${n}`).join(", ")})`, f, l);
    }
  }
});
