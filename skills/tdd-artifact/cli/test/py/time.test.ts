// Stamps: TDD_GENERATED pins `generated`; the UTC and local formats match CPython's strftime for 209 timestamps.
import { describe, expect, test } from "bun:test";
import { formatLocalMinute, formatUtc, utcStamp } from "../../src/py/time";
import { SKIP_REASON, hasPython, oracle } from "./oracle";

describe("time", () => {
  test("utcStamp honours TDD_GENERATED, else %Y-%m-%dT%H:%M:%SZ now", () => {
    const saved = process.env.TDD_GENERATED;
    try {
      process.env.TDD_GENERATED = "2026-01-02T03:04:05Z";
      expect(utcStamp()).toBe("2026-01-02T03:04:05Z");
      process.env.TDD_GENERATED = "";
      expect(utcStamp(new Date(Date.UTC(2026, 10, 9, 1, 2, 3, 999)))).toBe("2026-11-09T01:02:03Z");
      delete process.env.TDD_GENERATED;
      expect(utcStamp()).toMatch(/^\d{4}-\d\d-\d\dT\d\d:\d\d:\d\dZ$/);
    } finally {
      if (saved === undefined) delete process.env.TDD_GENERATED;
      else process.env.TDD_GENERATED = saved;
    }
  });
});

describe.skipIf(!hasPython)("time vs CPython", () => {
  if (!hasPython) console.warn(SKIP_REASON);
  test("UTC and local-minute formats", () => {
    // bun test runs in its own default zone; give the oracle the same one
    const tz = Intl.DateTimeFormat().resolvedOptions().timeZone;
    const g = oracle<{ rows: [number, string, string][] }>("stamp", [], { TZ: tz });
    const bad = g.rows.filter(([t, u, l]) => formatUtc(new Date(Math.floor(t) * 1000)) !== u || formatLocalMinute(t) !== l).slice(0, 5);
    expect(bad).toEqual([]);
  }, 60_000);
});
