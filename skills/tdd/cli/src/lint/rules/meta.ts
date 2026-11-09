// META: meta.json's header fields; rev or updated moved when the source changed after the last build; a published
// (non-draft) doc names its artifact_url (lint_packet.Lint.r_meta). The build time prints in local time (Clock).
import { Effect } from "effect";
import * as py from "../../py";
import { Packet } from "../packet";
import { ISO_DATE } from "../patterns";
import { Clock } from "../services";
import { type PyValue, dget, eq, get, isDict, isOsOrValueError, iter, repr, str, truthy } from "../pyops";

const re = py.re;
const { join } = py.path;

export const meta = Effect.gen(function* () {
  const p = yield* Packet;
  const clock = yield* Clock;
  const mp = join(p.root, "meta.json");
  const m = p.meta;
  if (m === null) {
    if (!p.fs.isfile(mp)) p.add("META", "missing", "error", "the packet has no meta.json (title, people, updated, rev, status)", null);
    return;
  }
  const at = (k: string) => p.locate('"' + k + '"', [mp])[1];
  const title = dget(m, "title");
  if (!(typeof title === "string" && py.str.strip(title))) {
    p.add("META", "field", "error", "meta.json has no title (the browser tab title)", "meta.json", at("title"));
  }
  const people = dget(m, "people");
  const hasAuthor = Array.isArray(people) && people.some((q) => isDict(q) && py.lower(str(get(q, "role", ""))) === "author" && truthy(get(q, "name")));
  if (!hasAuthor) p.add("META", "field", "error", "meta.json people has no Author (people: [{role: Author, name: ...}])", "meta.json", at("people"));
  const updated = dget(m, "updated");
  if (!(typeof updated === "string" && ISO_DATE.match(updated))) {
    p.add("META", "field", "error", `meta.json updated ${repr(updated)} is not an ISO date (YYYY-MM-DD)`, "meta.json", at("updated"));
  }
  const rev = dget(m, "rev");
  if (!(typeof rev === "number" || typeof rev === "bigint")) {
    p.add("META", "field", "error", `meta.json rev ${repr(rev)} is not an integer`, "meta.json", at("rev"));
  }
  const status = dget(m, "status");
  if (!(typeof status === "string" && py.str.strip(status))) {
    p.add("META", "field", "error", "meta.json has no status (e.g. 'Draft for review', 'Reviewed', 'As built')", "meta.json", at("status"));
  }
  // rev / updated must move when the source changed after the last build
  const cpath = join(p.build, "content.json");
  if (!p.compiled && p.fs.isfile(cpath) && truthy(p.content)) {
    const built = p.fs.getmtime(cpath);
    const srcs = [...p.text_files(), mp].filter((q) => q && p.fs.isfile(q));
    for (const sub of ["schema", "diagrams", "mockups"]) {
      const d = join(p.root, sub);
      if (p.fs.isdir(d)) for (const f of p.fs.listdir(d)) srcs.push(join(d, f));
    }
    const newer = srcs.filter((q) => p.fs.getmtime(q) > built + 1)
      .map((q) => [q, p.fs.getmtime(q)] as const)
      .sort((a, b) => (a[1] < b[1] ? 1 : a[1] > b[1] ? -1 : 0))
      .map(([q]) => q);
    if (newer.length) {
      const names = newer.slice(0, 4).map((q) => p.rel(q)).join(", ") + (newer.length > 4 ? ` and ${newer.length - 4} more` : "");
      p.add("META", "stale-build", "warn", `${names} changed after the last build (${clock.localMinute(built)}): ` +
        `rebuild before publishing (this lint read the old build)`, p.rel(cpath));
      const text_changed = newer.filter((q) => q.endsWith(".md"));
      const cm = dget(p.content, "meta", new Map());
      if (text_changed.length && eq(dget(cm, "rev"), dget(m, "rev")) && eq(dget(cm, "updated"), dget(m, "updated"))) {
        p.add("META", "rev-not-moved", "warn", `the source changed since the build of rev ${str(dget(m, "rev"))} (${str(dget(m, "updated"))}) but meta.json ` +
          `rev and updated did not move: bump rev and set updated (and rev_note) for this revision`, "meta.json", at("rev"));
      }
    }
  }
  // a published (non-draft) doc names its artifact
  if (typeof status === "string" && py.str.strip(status) && !py.lower(py.str.strip(status)).startsWith("draft")) {
    const urls: PyValue[] = [dget(m, "artifact_url")];
    const st = join(p.root, "status.md");
    if (p.fs.isfile(st)) {
      const mm = re.search(String.raw`artifact_url:\s*(https?://\S+)`, p.fs.readText(st));
      urls.push(mm ? mm.group(1) : null);
    }
    const pk = join(p.root, "packet.json");
    if (p.fs.isfile(pk)) {
      try {
        urls.push(dget(py.json.loads(p.fs.readText(pk)), "artifact_url"));
      } catch (e) {
        if (!isOsOrValueError(e)) throw e;   // a packet.json root that is not a dict raises, as in the Python
      }
    }
    if (!urls.some((u) => typeof u === "string" && u.startsWith("http"))) {
      p.add("META", "artifact-url", "warn", `status is '${status}' but no artifact_url is recorded (meta.json, or \`artifact_url:\` in status.md)`,
        "meta.json", at("status"));
    }
  }
});

export { iter };
