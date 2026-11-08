---
milestone: "M4 · Agent-first building"
milestone_note: "adds one published binary, sindri-code-runner"
title: Code block in C# with Roslyn and a sandboxed runner
change: A Code block (`code`) compiles a C# method body with Roslyn, caches the assembly by sha256, and runs it in a sindri-code-runner process inside the Sindri sandbox; `sindri wf validate` compiles Code nodes
rollback: Revert the PR; a spec that uses `code` fails validation with SND-GRAPH unknown block, and the new binary is no longer published
---
# PR 9c: Code block

## Summary
Code runs C#, not JavaScript (owner, 2026-11-08). Config: `input` (a reference such as `{{jev}}`), `code` (a method body
that gets `JsonNode input` and returns any value), `repo?` and `branch?` (files the code may read, read-only), `timeout`
(default 5 s, max 60 s), `memoryMb` (default 256). Output: `result`, the returned value as JSON. Sindri wraps the body in
a static method and compiles it with Roslyn (`CSharpCompilation`) against a fixed reference set (the BCL,
`System.Text.Json`, `System.Linq`). It caches the assembly by the sha256 of the code and the reference set, so a second
run doesn't compile again. The assembly runs in a separate process, `sindri-code-runner`, inside the Sindri sandbox: no
network, the repo mounted read-only, nothing else writable, a memory limit, and a kill at the timeout. Input goes in on
stdin, the result comes back on stdout. A separate process is needed because .NET has no in-process sandbox. The runner
can't be NativeAOT (it loads a compiled assembly), so it is framework-dependent like the Sindri host. Compile errors
return as the node's error with line and column. `sindri wf validate` compiles every Code node, so a typo fails at save time.
Rollback: revert.

## Code changes
| path:line | change |
|---|---|
| src/Sindri/Aw.Sindri.Core/Graph/Blocks/CodeBlock.cs, CodeConfig.cs (new) | config and output above; picker label Code, group Logic; the validator calls the compiler and reports line and column |
| src/Sindri/Aw.Sindri.Infrastructure/Workflow/Code/CodeCompiler.cs, CompiledCodeCache.cs (new) | Roslyn compile of the wrapped method body; cache key = sha256 of the code and the reference set; compile errors as diagnostics |
| src/Sindri/Aw.Sindri.CodeRunner/ (new project, `sindri-code-runner`) | loads the cached assembly, reads `input` on stdin, writes `result` on stdout, exit codes for compile, timeout, memory and runtime errors; framework-dependent |
| src/Sindri/Aw.Sindri.Infrastructure/Workflow/Code/CodeRunnerProcess.cs, Core/Sandbox/Plan (process variant from PR 16 or a minimal one here) | runs the runner in the Sindri sandbox: no network, `repo` read-only, memory limit, kill at `timeout`; no launch secret |
| src/Sindri/Aw.Sindri.Host/Commands (`sindri wf validate`) | compile every Code node and print diagnostics |
| scripts/install-cs.sh, `SINDRI_BUILD_PATHSPECS`, scripts/tests/install-cs.test.sh | follow-up row: publish `sindri-code-runner` (framework-dependent) beside `sindri` and link it; add its project to the build pathspecs and the install test |
| src/Sindri/aot-smoke/run.sh, expected.txt | follow-up row: the smoke run lists the new binary and validates a spec with a Code node |
| deps.yaml, src/Sindri/Aw.Sindri.CodeRunner/packages.lock.json, scripts/sindri-build-inputs.sh | follow-up row: declare the new project and its edges; commit the lock file; add it to the Sindri build inputs |
| docs/workflows/blocks/code.md, docs/csharp/sindri-graph.md | the example from the block list (filter items by score) as the page's example spec |

## Graph context
- A first compile takes about a second; the cache makes the second run skip it.
- The runner shares the sandbox work of PR 16 (a process variant of `ISandbox`); if PR 16 has not landed, this PR adds the smallest process plan it needs and PR 16 reuses it.
- Untrusted data: `input` may hold connection text; the code sees it as data and its result is fenced wherever it reaches a prompt.
- Readers: `sindri wf validate`, `workflow_validate` over MCP, and the graph run itself.

## Tests
- CodeCompilerTests.A_body_with_a_typo_returns_a_diagnostic_with_line_and_column: no process started.
- CompiledCodeCacheTests.The_same_code_and_references_compile_once: second call hits the cache by sha256.
- CodeBlockTests.The_score_filter_example_returns_the_titles_over_100: runner against a fake sandbox.
- CodeRunnerTests.A_loop_past_the_timeout_is_killed_and_the_node_fails_with_timeout: time-bounded.
- CodeRunnerTests.Network_and_writes_outside_the_mounts_are_refused: sandbox plan assertion.
- WfValidateTests.Validate_compiles_every_Code_node_and_fails_on_a_typo: CLI exit code 1.
- InstallCsTests.The_code_runner_is_published_and_linked: scripts/tests/install-cs.test.sh row.
