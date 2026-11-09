# tdd-cli

The tdd skill's command line under bun (TypeScript 7, Effect 4 where a program is effectful). It is the target of the
bun port designed in the architecture packet's `research/bun-port-2026.md` (D19) and the home of the system-database
loader (D17, D23, D24) and the revision runner.

- `bin/tdd.ts` dispatches `tdd <command>`; each command is a `bin/<name>.ts` with a `main(argv): Promise<number>`.
  A command that is not ported yet says so and names the Python script to run instead.
- `src/py/` is the Python-semantics kernel (lossless JSON, float repr, `re`, `str`, `html`, `os.path`) that byte-identity
  with the Python scripts depends on; `src/sd/` is the TypeScript copy of the system-design surface the compiler uses,
  pinned by `UPSTREAM.json`; `src/compile`, `src/lint`, `src/agents`, `src/hotspots`, `src/eval`, `src/rev`, `src/db`
  are the ports and the new programs.
- `parity/` holds the Python oracle and `gate.ts`, which runs both implementations into the same path and diffs.
- `sql/schema.sql` is the system database (eight tables); `tdd load-db <packet>` fills it after lint passes.

Setup once: `bun install` here. Check: `bun run check`. Tests: `bun test`. During the overlap period the Python scripts
stay the documented path (SKILL.md); nothing here is required to build a TDD until the cutover PR.
