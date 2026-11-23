# Aw.Transcripts: the local transcript corpus

`Aw.Transcripts` (`src/Transcripts`, tier 1) reads every local agent transcript of Claude Code, Codex and Cursor as one
provider-neutral corpus. It reads files only. It calls no model and writes nothing. Issue 3787 builds on it: a later
PR adds `sindri transcripts list|show --json`, and a steering miner reads the human turns.

The port is `ITranscriptCorpus` (`Aw.Transcripts.Core.Ports`):

| Call | Returns |
|---|---|
| `ListSessions(SessionQuery)` | The sessions whose file was written at or after `Since`, newest first. Filters: provider, repo, subagents on or off |
| `ReadSession(SessionRef)` | One session's turns in file order, and the count of skipped lines |

The file adapter is `FileTranscriptCorpus`. A host registers it with `AddTranscriptsInfrastructure(environment)` for the
default roots, or with `AddTranscriptsInfrastructure(roots)` for its own list. The fake is `InMemoryTranscriptCorpus`
in `Aw.Transcripts.Testing`. Both pass `TranscriptCorpusContract`.

## What it reads

| Provider | Default root | Layout |
|---|---|---|
| `claude-code` | `~/.claude/projects` | `<project>/<session>.jsonl`; a subagent is `<project>/<session>/subagents/agent-<id>.jsonl` |
| `codex` | `~/.codex/sessions` | `YYYY/MM/DD/rollout-<time>-<thread id>.jsonl` |
| `cursor` | `~/.cursor/projects` | `<project>/agent-transcripts/<id>/<id>.jsonl`; a subagent is `<id>/subagents/<sub>.jsonl` |

The default roots also hold Sindri's separate agent homes: `$AW_SINDRI_HOME/agent-homes/claude-code/projects` and
`$AW_SINDRI_HOME/agent-homes/codex/sessions`. `AW_SINDRI_HOME` defaults to `$AW_STATE_DIR/sindri-app`. Core never reads
the environment: the Infrastructure registration reads `HOME`, `AW_STATE_DIR` and `AW_SINDRI_HOME` from the lambda the
host passes. A root that does not exist adds no session. That is not an error.

A list stats every file first. It opens only the files written since `Since`, plus the neighbours that dedupe needs
(below). The reader streams each file line by line, so a 100 MB transcript is never one string.

## A session

| Field | Meaning |
|---|---|
| `Ref` | The opaque session ref (below) |
| `StartedAt` | The first timestamp in the file. Cursor: the first prompt's `<timestamp>` block, else the file's write time |
| `LastWrittenAt` | The file's write time |
| `Repo` | The main checkout the session ran in, from the session's cwd (below). Null for Cursor |
| `IsSubagent`, `Parent` | Whether a parent spawned it, and the parent's ref when the parent's file is under the roots |
| `HumanTurns` | The prompts a person typed in this file, without the copies |

The repo comes from the first cwd in the file (Claude Code `cwd`, Codex `session_meta.cwd`). The resolver walks up
from the cwd to the nearest `.git`:

1. A `.git` directory: that directory's parent is the repo.
2. A `.git` file (`gitdir: <dir>`): the worktree's git dir names the main git dir in its `commondir` file. The main
   git dir's parent is the repo.
3. A cwd that no longer exists, or that sits in no repo, stays as it is.

## A turn

A turn has a ref, a role (`Human` or `Assistant`), a start time, its text and, for an assistant turn, its tool names.
Each tool name appears once, in first-use order. Tool input and tool output are never read.

An assistant turn is all the agent's lines from one prompt to the next. Tool results inside it do not split it. A
machine prompt (below) ends it.

### What counts as a human turn

A human turn is a prompt a person typed. These are machine text, never human turns:

- Tool results.
- Tag-led text (after the leading `<ide_opened_file>` and `<ide_selection>` blocks an IDE extension adds, which are
  stripped; what follows them is judged by these rules, and the stored prompt leaves them out): `<system-reminder>`, `<command-name>` and the other slash-command wrappers, `<local-command-stdout>`,
  `<teammate-message>`, `<task-notification>`, `<environment_context>`. A lone `<pasted_content>` block is still typed.
- Compaction summaries ("This session is being continued…"), skill preambles ("Base directory for this skill"),
  "Caveat:" notes, peer-session deliveries ("Another Claude session sent a message"), interrupt markers, AGENTS.md text.
- Claude Code lines flagged `isMeta`, `isSidechain`, `isCompactSummary` or `isVisibleInTranscriptOnly`, and lines whose
  `origin.kind` is anything but `human` (`peer`, `task-notification`). Older lines with no `origin` pass this rule.
- Every user line of a subagent file: its first one is the parent's task.
- Scripted runs: a Claude Code line whose `entrypoint` starts with `sdk` (`claude -p`), and a Codex session whose
  `session_meta.source` is `exec`. Scripts and Sindri start these as often as a person does. This is a choice; revisit
  it if the steering miner misses real prompts.

Codex writes each prompt twice. The reader takes the `event_msg` `user_message` and skips the `response_item` user
messages, which wrap the prompt in AGENTS.md and environment context.

Cursor wraps the prompt in `<user_query>` blocks. The reader takes their text, or, with none, the text after the
leading tag blocks.

### Scrubbing

Every turn's text goes through the `Aw.Redaction` scrubber (`IRedactor`, `Redactor.Default` unless the host registers
its own) before it leaves the library. A secret becomes a `[REDACTED:<rule>]` placeholder.

## Refs

| Ref | Shape | Made from |
|---|---|---|
| Session | `transcript:<12 hex>` | The first 6 bytes of SHA-256 of the provider's wire name, a newline and the file's full path |
| Turn | `transcript:<12 hex>#<line>` | The session ref and the 1-based line the turn starts on |

A ref names no project, repo or path. It stays the same across runs while the file stays where it is. `ReadSession`
resolves a ref by hashing the path of every file under the roots, without opening any. A turn's session ref is `TurnRef.SessionOf()`: a method, because a derived typed-id property breaks `ARCH-ID-002` (a typed-id property must be required on read). Two files with one ref are
`AW-TRANSCRIPTS-004`; with 12 hex digits (48 bits) a collision stays below one in a million up to about 20,000 files.

## Resume and fork dedupe

A resumed or forked Claude Code session copies earlier lines into a new file with their old timestamps. The same prompt
must appear once in the corpus, so:

1. The key of a turn is the SHA-256 of its role, its start time, its whitespace-normalized text and its tools.
2. Files order by first timestamp, then last timestamp, then path. A resume starts with the original's lines but runs
   longer, so the original sorts first.
3. A turn whose key a file beside it (same directory, same provider) holds, where that file sorts earlier, is a copy.
   The corpus drops it from `ReadSession` and from `HumanTurns`. A file never dedupes against itself.

A line stamped at time t is written at t or later. So only a neighbour written at or after the target's first line can
hold a copy, and only those neighbours are read. This holds even when the original is older than `Since`.

Example: `main.jsonl` holds the prompt "fix the login bug" at 10:00. `fork.jsonl` copies that line and adds "ship it"
at 10:40. `ListSessions` gives main 1 human turn and fork 1 human turn, not 2.

Dedupe runs only inside one directory. A Codex resume in a later day's folder is not deduped (no case of it seen yet).

## Errors

| Code | When |
|---|---|
| `AW-TRANSCRIPTS-001` | Invalid input (a relative repo path in `ListSessions`) |
| `AW-TRANSCRIPTS-002` | No file under the roots has the ref |
| `AW-TRANSCRIPTS-003` | The ref's file exists but can't be read |
| `AW-TRANSCRIPTS-004` | Two files share the ref |

A line that is not a JSON object of the provider's shape is skipped and counted in `SkippedLines`. That includes lines
of a known kind whose fields have an unexpected type, a line with no type (Claude Code, Codex) or no payload (Codex), a
user or assistant line whose content is no string or block array, and a Cursor line with neither a user or assistant
role nor a type. A known bookkeeping line (a Claude Code `permission-mode`, a Cursor `turn_ended`) is not counted. A list leaves out a file it can't read (deleted or locked
mid-scan) and lists the rest. A bad field never fails a list: a cwd that is no valid path (a NUL byte) gives the
session no repo, and a line whose pattern match runs out of time (Cursor's prompt and timestamp patterns) is skipped.

## Not verified

- **Cursor.** This box has no real Cursor transcripts. The reader follows `config/hooks/tests/fixtures/cursor/transcript.jsonl`
  and the notes in the old TypeScript scorer (`scorer/src/transcript/cursor.ts`, deleted in `ec2814b96b`). Not seen in
  real files: the `<timestamp>` and `<user_query>` wrappers, the subagent folder, the flat `agent-transcripts/<id>.jsonl`
  form, and whether a line ever names a cwd. Cursor sessions get no repo.
- **Codex imports.** A Codex thread imported from Claude Code (`~/.codex/external_agent_session_imports.json`) repeats
  that session's prompts. They are not deduped against the Claude Code file.
- **Codex `archived_sessions`.** Not a default root. Pass it as an extra root to read it.
