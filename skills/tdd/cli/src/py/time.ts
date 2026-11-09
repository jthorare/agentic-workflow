// Wall-clock stamps. `generated` in content.json is the one volatile field of a compile: TDD_GENERATED pins it for
// both compilers (research/bun-port-2026.md R17), so two compiles of one source are byte-identical.

const pad = (n: number, w = 2) => String(n).padStart(w, "0");

/** datetime.now(timezone.utc).strftime("%Y-%m-%dT%H:%M:%SZ"), or $TDD_GENERATED verbatim when it is set and not empty. */
export function utcStamp(now: Date = new Date()): string {
  const pinned = process.env.TDD_GENERATED;
  if (pinned) return pinned;
  return formatUtc(now);
}

/** "%Y-%m-%dT%H:%M:%SZ" in UTC (no milliseconds, unlike toISOString). */
export function formatUtc(d: Date): string {
  return `${pad(d.getUTCFullYear(), 4)}-${pad(d.getUTCMonth() + 1)}-${pad(d.getUTCDate())}T${pad(d.getUTCHours())}:${pad(d.getUTCMinutes())}:${pad(d.getUTCSeconds())}Z`;
}

/** f"{datetime.fromtimestamp(t):%Y-%m-%d %H:%M}": local time, as lint's stale-build message prints it. */
export function formatLocalMinute(seconds: number): string {
  const d = new Date(Math.floor(seconds) * 1000);
  return `${pad(d.getFullYear(), 4)}-${pad(d.getMonth() + 1)}-${pad(d.getDate())} ${pad(d.getHours())}:${pad(d.getMinutes())}`;
}
