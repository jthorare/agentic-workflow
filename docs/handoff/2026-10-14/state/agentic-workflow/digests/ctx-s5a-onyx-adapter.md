# S-5A onyx adapter — handoff (2026-10-12)
Worktree ~/repos/agentic-workflow/.claude/worktrees/ctx-s5a (branch feat/ctx-s5a).
Done: 466621b3 red fake tests; 3ae5afa8 fake (admin search, size-info, /me, Delay, README); 51b3370e red adapter tests + stubs.
Left: implement Core/Onyx/OnyxRecordKeys (Identify, KindOf) -> commit feat; implement Infrastructure/Onyx:
OnyxRequests.Send (allowlist exact method+route, bearer, single send path; OnyxVersionProbe should use it),
OnyxClient(settings, parts) Call<T> mapping (002 token unresolved/bad chars, 010 down/timeout/5xx, 050 403,
052 404 on document routes, 051 other/shape), wire records in OnyxWireJsonContext (snake_case names),
OnyxSource : IContextSource (Search: quoted terms, one admin search, ranks, Limit client-side, Leaky if updated_at>AsOf,
hit.AsOf=clock now, Version=updated_at UTC "O"; memo hit facts for Fetch; Fetch size-info then chunk-info, "\n\n",
64KB cap; Versions -> 010 no request; Probe version then /api/me: admin+SERVICE_ACCOUNT Ok "admin, service account",
admin other -> Degraded 053, no admin -> Unavailable 050), OnyxSources.Create. LoggerMessage logs route/status/code.
Tests: tests/Aw.Context.Tests/Onyx/*. Then build -warnaserror, unit+integration lanes for Context.Tests,
Context.Testing.Tests, Architecture.Tests (lock file update), dotnet format, node scripts/check-deps.mjs.
Status: COMPLETE (5a11cbc0). No work left.
