// Where a revision reads and writes: the packet, its build folder and build/.rev, the source markdown, and the skill's
// own folders (cli/, app/, scripts/) found from this file. artifact_check.mjs is looked up the way build_tdd.py's
// sd_root() and the eval's artifact_check_path() do it.
import { existsSync, readdirSync, readFileSync, statSync } from "node:fs";
import { homedir } from "node:os";
import { basename, dirname, join, resolve } from "node:path";

export interface Paths {
  /** The packet folder, absolute. */
  readonly packet: string;
  /** <packet>/build. */
  readonly build: string;
  /** <packet>/build/.rev: the journal, the state and the lock. */
  readonly rev: string;
  /** The packet folder's name: the slug. */
  readonly slug: string;
  /** The source markdown relative to the packet (as SKILL.md passes it, from the packet folder), or null. */
  readonly source: string | null;
  /** The skill folder (holds SKILL.md, app/, cli/, scripts/). */
  readonly skill: string;
  readonly cli: string;
  readonly app: string;
}

const CLI = resolve(import.meta.dir, "..", "..");

export function resolvePaths(packetArg: string, skill = dirname(CLI)): Paths {
  const packet = resolve(packetArg);
  const build = join(packet, "build");
  return {
    packet, build, rev: join(build, ".rev"), slug: basename(packet), source: findSource(packet, build),
    skill, cli: join(skill, "cli"), app: join(skill, "app"),
  };
}

const isFile = (p: string): boolean => {
  try { return statSync(p).isFile(); } catch { return false; }
};

/**
 * lint_packet.find_source: <slug>.md, else content.json's `source`, else the only top-level .md that is not
 * status.md, review* or README*. Relative to the packet.
 */
export function findSource(packet: string, build: string): string | null {
  const slug = basename(packet);
  if (isFile(join(packet, slug + ".md"))) return slug + ".md";
  try {
    const c = JSON.parse(readFileSync(join(build, "content.json"), "utf8")) as { source?: unknown };
    if (typeof c.source === "string" && c.source && isFile(join(packet, c.source))) return c.source;
  } catch {
    // no build yet, or not JSON: fall through to the listing
  }
  let names: string[] = [];
  try { names = readdirSync(packet).sort(); } catch { return null; }
  const mds = names.filter((f) => f.endsWith(".md") && f !== "status.md" && !f.startsWith("review") && !f.startsWith("README"));
  return mds.length === 1 ? mds[0]! : null;
}

/**
 * The system-design checker: $SYSTEM_DESIGN_SKILL, the sibling skills/system-design, ~/.claude/skills/system-design,
 * then the skill's vendor/system-design; the first that has scripts/artifact_check.mjs (upstream's system-design has
 * none, so it falls through to the vendored copy).
 */
export function artifactCheckPath(skill: string, env: Record<string, string | undefined> = process.env): string | null {
  const candidates = [env["SYSTEM_DESIGN_SKILL"], join(skill, "..", "system-design"), join(homedir(), ".claude", "skills", "system-design"),
    join(skill, "vendor", "system-design")];
  for (const c of candidates) {
    if (!c) continue;
    const p = join(c, "scripts", "artifact_check.mjs");
    if (isFile(p)) return resolve(p);
  }
  return null;
}

/** A path for messages: ~ for the home folder, so the journal and the table never print a user's absolute path. */
export function homeRel(p: string): string {
  const h = homedir();
  return p === h ? "~" : p.split(h + "/").join("~/");
}

export const exists = existsSync;
