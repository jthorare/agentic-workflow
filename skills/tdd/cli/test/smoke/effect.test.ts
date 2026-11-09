// The TypeScript 7 + Effect 4 gate (research/bun-port-2026.md section 4.4): a Layer, a Schema class, Effect.forEach
// with bounded concurrency and a scoped resource, on effect 4.1.2 under bun. `bun run check` type-checks this file
// with typescript 7.0.2 (tsconfig includes test/); `bun test` runs it. If either fails, the linter and the eval are
// written in plain TypeScript and the failure is recorded in README.md under "Effect smoke".
import { expect, test } from "bun:test";
import { Context, Effect, Exit, Layer, Schema } from "effect";

// A Schema class: validated construction and decoding of an unknown value.
class Finding extends Schema.Class<Finding>("Finding")({
  rule: Schema.String,
  check: Schema.String,
  line: Schema.Number,
}) {
  get key(): string {
    return `${this.rule}/${this.check}:${this.line}`;
  }
}

// A service and a Layer that provides it. The layer opens a scoped resource whose release is observable.
class Store extends Context.Service<Store, {
  readonly put: (f: Finding) => Effect.Effect<void>;
  readonly all: () => Effect.Effect<ReadonlyArray<string>>;
}>()("tdd/smoke/Store") {}

const events: string[] = [];

const StoreLive = Layer.effect(
  Store,
  Effect.gen(function* () {
    const rows = yield* Effect.acquireRelease(
      Effect.sync(() => {
        events.push("open");
        return [] as string[];
      }),
      (r) => Effect.sync(() => events.push(`close ${r.length}`)),
    );
    return {
      put: (f: Finding) => Effect.sync(() => void rows.push(f.key)),
      all: () => Effect.sync(() => [...rows]),
    };
  }),
);

const decode = Schema.decodeUnknownEffect(Finding);

const program = Effect.gen(function* () {
  const store = yield* Store;
  let live = 0;
  let peak = 0;
  const inputs: unknown[] = [1, 2, 3, 4, 5, 6].map((n) => ({ rule: "REFS", check: "title-token", line: n }));
  yield* Effect.forEach(
    inputs,
    (raw) =>
      Effect.gen(function* () {
        live++;
        peak = Math.max(peak, live);
        const f = yield* decode(raw);
        yield* Effect.sleep("5 millis");
        yield* store.put(f);
        live--;
      }),
    { concurrency: 3, discard: true },
  );
  const rows = yield* store.all();
  return { rows, peak };
});

test("effect 4: Layer, Schema class, forEach with concurrency, scoped resource", async () => {
  events.length = 0;
  const out = await Effect.runPromise(Effect.provide(program, StoreLive));
  expect([...out.rows].sort()).toEqual([1, 2, 3, 4, 5, 6].map((n) => `REFS/title-token:${n}`));
  expect(out.peak).toBe(3);
  // the layer's resource was opened once and released when the program's scope closed
  expect(events).toEqual(["open", "close 6"]);
});

test("effect 4: a decode failure is a typed failure, not a throw", async () => {
  const exit = await Effect.runPromiseExit(decode({ rule: "REFS", check: 7, line: "x" }));
  expect(Exit.isFailure(exit)).toBe(true);
  expect(new Finding({ rule: "A", check: "b", line: 2 }).key).toBe("A/b:2");
});
