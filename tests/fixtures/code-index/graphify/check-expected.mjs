// Compares each fixture's recorded graph.json with expected-calls.json: node check-expected.mjs [dir] (this directory by default).
// Prints one row per expected call (found, the edge's confidence, whether both ends carry file and line) and a found/expected total per language.
import { readFileSync } from "node:fs";
import { dirname, join } from "node:path";
import { fileURLToPath } from "node:url";

const dir = process.argv[2] ?? dirname(fileURLToPath(import.meta.url));
const expected = JSON.parse(readFileSync(join(dir, "expected-calls.json"), "utf8"));
let unexpectedMiss = false;
for (const [language, calls] of Object.entries(expected)) {
  const graph = JSON.parse(readFileSync(join(dir, language, "graph.json.recorded"), "utf8"));
  const byId = new Map(graph.nodes.map((n) => [n.id, n]));
  const classOf = new Map(graph.links.filter((l) => l.relation === "method").map((l) => [l.target, byId.get(l.source).label]));
  const qualified = (n) => {
    const bare = n.label.replace(/\(\)$/, "");
    return bare.startsWith(".") ? `${classOf.get(n.id) ?? "?"}${bare}` : bare;
  };
  const hasPlace = (n) => typeof n.source_file === "string" && /^L\d+$/.test(n.source_location ?? "");
  let found = 0;
  console.log(`${language}`);
  for (const c of calls) {
    const edge = graph.links.find((l) => {
      if (l.relation !== "calls") return false;
      const s = byId.get(l.source), t = byId.get(l.target);
      return qualified(s) === c.caller && s.source_file === c.callerFile && qualified(t) === c.callee && t.source_file === c.calleeFile;
    });
    if (edge) found++;
    const ends = edge && hasPlace(byId.get(edge.source)) && hasPlace(byId.get(edge.target)) ? "file+line on both ends" : "-";
    if (!edge && !c.knownMiss) unexpectedMiss = true;
    console.log(`  ${edge ? "found  " : c.knownMiss ? "KNOWN MISS" : "MISSING"} ${c.caller} -> ${c.callee}  ${edge ? `${edge.confidence} at ${edge.source_file}:${edge.source_location}; ${ends}` : ""}`);
  }
  console.log(`  ${found}/${calls.length}`);
}
// Exit 0 when only the calls marked knownMiss are missing, 1 on any other miss.
process.exit(unexpectedMiss ? 1 : 0);
