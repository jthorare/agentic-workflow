#!/usr/bin/env node
// scripts/check-profile-doc.mjs — docs/csharp/sindri-profile.md names every key of the C# SindriProfile (spec 2026-10-15
// session-launcher L-8, phase SL-0).
//
//   node scripts/check-profile-doc.mjs [--root DIR]
//
// Reads the sealed records of src/Sindri/Aw.Sindri.Core/Profile/*.cs that refuse unknown keys
// ([JsonUnmappedMemberHandling(Disallow)]: SindriProfile and the records it nests), camel-cases their constructor
// parameters (the profile's key names) and fails for each one docs/csharp/sindri-profile.md doesn't show as `key`.
// Not a C# test on purpose: a new profile key in another lane's PR fails this doc check with its fix line, never the
// C# unit lane.
// Exit codes: 0 ok, 1 a key is missing, 2 a tool error. Node builtins only.

import fs from "node:fs";
import path from "node:path";
import { fileURLToPath } from "node:url";

const args = process.argv.slice(2);
const rootArg = args.indexOf("--root");
const ROOT = rootArg >= 0 ? path.resolve(args[rootArg + 1]) : path.join(path.dirname(fileURLToPath(import.meta.url)), "..");
const DIR = path.join(ROOT, "src/Sindri/Aw.Sindri.Core/Profile");
const DOC = path.join(ROOT, "docs/csharp/sindri-profile.md");

const camel = (s) => s[0].toLowerCase() + s.slice(1);

/** The constructor parameter names of every `[JsonUnmappedMemberHandling(...Disallow)] public sealed record X(...)`. */
export function recordKeys(source) {
  const out = [];
  const re = /\[JsonUnmappedMemberHandling\(JsonUnmappedMemberHandling\.Disallow\)\]\s*public sealed record (\w+)\(/g;
  for (let m; (m = re.exec(source)); ) {
    let depth = 1;
    let start = re.lastIndex;
    const params = [];
    for (let i = re.lastIndex; i < source.length && depth > 0; i++) {
      const c = source[i];
      if ("(<[{".includes(c)) depth++;
      else if (")>]}".includes(c)) {
        depth--;
        if (depth === 0) params.push(source.slice(start, i));
      } else if (c === "," && depth === 1) {
        params.push(source.slice(start, i));
        start = i + 1;
      }
    }
    for (const p of params) {
      const text = p.replace(/\[[^\]]*\]\s*/g, "").split("=")[0].trim();
      const name = text.split(/\s+/).pop();
      if (name) out.push({ record: m[1], key: camel(name) });
    }
  }
  return out;
}

try {
  if (!fs.existsSync(DIR)) throw new Error(`${DIR} not found`);
  if (!fs.existsSync(DOC)) throw new Error(`${DOC} not found`);
  const doc = fs.readFileSync(DOC, "utf8");
  const keys = fs.readdirSync(DIR).filter((f) => f.endsWith(".cs")).flatMap((f) => recordKeys(fs.readFileSync(path.join(DIR, f), "utf8")));
  if (!keys.some((k) => k.record === "SindriProfile")) throw new Error("found no SindriProfile record");
  const missing = keys.filter((k) => !doc.includes(`\`${k.key}\``));
  for (const k of missing) console.error(`FAIL ${k.record}.${k.key} is not in docs/csharp/sindri-profile.md: add ${k.key} to docs/csharp/sindri-profile.md (a row with the key in backticks)`);
  if (missing.length) process.exit(1);
  console.log(`check-profile-doc: ok (${keys.length} keys)`);
} catch (e) {
  console.error(`check-profile-doc: ${e.message}`);
  process.exit(2);
}
