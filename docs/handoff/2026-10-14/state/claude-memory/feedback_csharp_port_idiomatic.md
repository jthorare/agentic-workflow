---
name: csharp-port-idiomatic
description: "R2 C# port is a reimplementation in idiomatic C# with TS as a guideline only; compatibility target is just what the bash hooks read"
metadata:
  node_type: memory
  type: feedback
  originSessionId: 2605b142-e047-4652-bb12-8324a32cfebc
  modified: 2026-10-10T23:16:29.912Z
---

The TS→C# port (R2 packets: Judge, Scorer, Evidence, BugFix, Sindri) is a cutover and a reimplementation in idiomatic
C#, with the TS implementation as a guideline for WHAT a package does, not HOW. No backward compatibility and no TS
quirks or hacks (no JS-semantics JSON layer, no per-field lenient parsing copied from TS, no "reason_code prefix"
style markers, no silent exit codes kept "because TS did"). Input JSON is type-driven: records deserialized through
AwJson, not JsonDocument walking. The only compatibility target is what the bash hooks and statusline that keep running
actually read (command names/argv, the specific fields they jq, exit codes they branch on, stderr prefixes they grep,
files they write). Tier-2 callers (ui-evidence, bugfix) are ported too, so they follow the C# contract, not TS.

**Why:** owner, 2026-10-10, on R2-J1, said it three times as I kept drifting: "We were going to just cutover to C# not
looking for JS parity", "why are we parsing JsonDocuments instead of … type driven parsing", and "This is a cutover
and reimplementation with the TS implementation as a guideline. Not a direct port with these weird hacks. We want
idiomatic C#".

**How to apply:** in port plans and agent briefs, never write "exactly as TS", "byte for byte", or "match TS
stdout/exit" for anything but the hook-read contract; define contracts in the C# package's own doc and tests; list
TS quirks to drop rather than reproduce. Related: [[review-loop-severity]].
