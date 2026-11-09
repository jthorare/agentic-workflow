// SCHEMA: schema/*.json against the D7 table contract; measurements carry a source and a date, expectations a basis;
// every table is used by a diagram hotspot or named on the page (lint_packet.Lint.r_schema).
import { Effect } from "effect";
import * as py from "../../py";
import { Packet } from "../packet";
import { ISO_DATE } from "../patterns";
import { type PyDict, type PyValue, dget, eq, get, has, inTuple, isDict, isOsOrValueError, items, iter, plain, str, strip, truthy, walk } from "../pyops";

const re = py.re;
const { join } = py.path;
const DIGIT = re.compile(String.raw`\d`);

export const schema = Effect.gen(function* () {
  const p = yield* Packet;
  const sdir = join(p.root, "schema");
  if (!p.fs.isdir(sdir)) return;
  const c = truthy(p.content) ? p.content : new Map();
  const parts: string[] = [];
  for (const [, , k, v] of walk(get(c, "sections", []))) if (k !== "src" && k !== "href") parts.push(plain(v));
  const page_text = parts.join(" ");
  const hs_tables = new Set<unknown>();
  const ddir = join(p.root, "diagrams");
  for (const f of p.fs.isdir(ddir) ? py.sorted(p.fs.listdir(ddir)) : []) {
    if (!f.endsWith(".hotspots.json")) continue;
    let spec: PyValue;
    try {
      spec = py.json.loads(p.fs.readText(join(ddir, f)));
    } catch (e) {
      if (!isOsOrValueError(e)) throw e;
      continue;
    }
    // outside the Python's try only in form: its handler catches ValueError and OSError, which these lines never raise
    for (const b of iter(dget(spec, "boxes", []))) if (truthy(dget(b, "table"))) hs_tables.add(dget(b, "table"));
  }
  for (const fn of py.sorted(p.fs.listdir(sdir))) {
    if (!fn.endsWith(".json")) continue;
    const path = join(sdir, fn);
    const rp = p.rel(path);
    const t = p.load_json(path, "SCHEMA");
    if (t === null) continue;
    if (!isDict(t)) {
      p.add("SCHEMA", "shape", "error", `${fn} is not a JSON object`, rp, 1);
      continue;
    }
    const at = (s: string) => p.locate(s, [path])[1];
    for (const k of ["table", "state", "columns"]) {
      if (!t.has(k)) p.add("SCHEMA", "required", "error", `${fn} has no '${k}' (required: table, state, columns[])`, rp, 1);
    }
    const name = str(truthy(get(t, "table")) ? get(t, "table") : py.cpSlice(fn, 0, -5));
    const state = get(t, "state");
    if (t.has("state") && !inTuple(state, ["new", "changed", "existing"])) {
      p.add("SCHEMA", "state", "error", `${name}: state '${str(state)}' is not new, changed or existing`, rp, at('"state"'));
    }
    let cols: PyValue = get(t, "columns");
    if (t.has("columns") && !Array.isArray(cols)) {
      p.add("SCHEMA", "required", "error", `${name}: columns is not a list`, rp, at('"columns"'));
      cols = [];
    }
    const names: PyValue[] = [];
    for (const col of iter(truthy(cols) ? cols : [])) {
      if (!isDict(col) || !truthy(get(col, "name"))) {
        p.add("SCHEMA", "column", "error", `${name}: a column has no name`, rp, at('"columns"'));
        continue;
      }
      const cname = col.get("name")!;
      names.push(cname);
      const cl = at('"name": ' + py.json.dumps(cname));
      if (!truthy(get(col, "type")) && !strip(truthy(get(col, "note")) ? get(col, "note") : "")) {
        p.add("SCHEMA", "column", "warn", `${name}.${str(cname)} has no type and no note saying why (D7: write the type, or note ` +
          `that the source does not state it)`, rp, cl);
      }
      if (col.has("change") && !inTuple(col.get("change"), ["new", "changed", "dropped", "unchanged"])) {
        p.add("SCHEMA", "column", "error", `${name}.${str(cname)}: change '${str(col.get("change"))}' is not new, changed, dropped or unchanged`, rp, cl);
      }
      if (eq(get(col, "change"), "changed") && !truthy(get(col, "was"))) {
        p.add("SCHEMA", "column", "warn", `${name}.${str(cname)} is changed but has no 'was' (what it is today)`, rp, cl);
      }
      if (eq(state, "new") && inTuple(get(col, "change"), ["changed", "dropped", "unchanged"])) {
        p.add("SCHEMA", "column", "error", `${name} is a new table but column ${str(cname)} is '${str(get(col, "change"))}'`, rp, cl);
      }
    }
    if (eq(state, "changed") && truthy(cols) && iter(cols).every((x) => isDict(x) && eq(get(x, "change", "unchanged"), "unchanged"))) {
      p.add("SCHEMA", "column", "warn", `${name} is 'changed' but no column is new, changed or dropped`, rp, at('"state"'));
    }
    const rows = get(t, "sample_rows");
    iter(truthy(rows) ? rows : []).forEach((row, i0) => {
      const i = i0 + 1;
      if (!isDict(row)) {
        p.add("SCHEMA", "sample-rows", "error", `${name}: sample row ${i} is not an object`, rp, at('"sample_rows"'));
        return;
      }
      const extra = [...row.keys()].filter((k) => !names.some((n) => eq(k, n)));
      if (extra.length) {
        p.add("SCHEMA", "sample-rows", "error", `${name}: sample row ${i} has keys that are not columns: ${extra.join(", ")}`, rp,
          at('"' + extra[0] + '"') ?? at('"sample_rows"'));
      }
    });
    if (truthy(rows) && !truthy(get(t, "sample_source"))) {
      p.add("SCHEMA", "sample-rows", "warn", `${name}: sample_rows have no sample_source (where they came from, or 'illustrative')`, rp, at('"sample_rows"'));
    }
    const measured = get(t, "measured");
    for (const [k, m] of items(truthy(measured) ? measured : new Map())) {
      if (!isDict(m)) {
        p.add("SCHEMA", "measured", "error", `${name}: measured.${k} is not an object`, rp, at('"' + k + '"'));
        continue;
      }
      if (get(m, "value") === null) continue;
      const miss = ["source", "date"].filter((x) => !truthy(get(m, x)));
      if (miss.length) {
        p.add("SCHEMA", "measured", "error", `${name}: measured.${k} = ${str(get(m, "value"))} has no ${miss.join(" or ")} (D7: a measurement names where ` +
          `and when it was taken; otherwise leave it out and the drawer reads 'not measured')`, rp, at('"' + k + '"'));
      } else if (!ISO_DATE.match(str(get(m, "date")))) {
        p.add("SCHEMA", "measured", "error", `${name}: measured.${k} date '${str(get(m, "date"))}' is not YYYY-MM-DD`, rp, at('"' + k + '"'));
      }
    }
    const expected = get(t, "expected");
    for (const [k, e] of items(truthy(expected) ? expected : new Map())) {
      if (!isDict(e)) {
        p.add("SCHEMA", "expected", "error", `${name}: expected.${k} is not an object`, rp, at('"' + k + '"'));
        continue;
      }
      const v = get(e, "value");
      const numeric = typeof v === "number" || typeof v === "bigint" || v instanceof py.PyFloat || (typeof v === "string" && DIGIT.search(v) !== null);
      if (numeric && !strip(truthy(get(e, "note")) ? get(e, "note") : "")) {
        p.add("SCHEMA", "expected", "error", `${name}: expected.${k} = ${str(v)} has no note giving its basis (D7: an expectation says how it was ` +
          `estimated)`, rp, at('"' + k + '"'));
      }
    }
    if (!hs_tables.has(name) && !page_text.includes(name)) {
      p.add("SCHEMA", "unused", "warn", `table ${name} is neither a hotspot table on a diagram nor named on the page`, rp, 1);
    }
  }
});

export { has };
export type { PyDict };
