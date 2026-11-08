---
milestone: "M5 · Connections"
milestone_note: "lands after PR 10b (sign-in)"
title: "Meetings block: granola and wispr-flow presets"
change: A Meetings block (`meetings`) reads one day's meetings from a note-app connection through IMeetingSource, with two presets, granola and wispr-flow, on the generic OAuth connections
rollback: Revert the PR; the presets are data rows and the block is a registry entry, so no other block depends on them
---
# PR 10d: Meetings block

## Summary
Config: `connection` (a meeting-notes connection), `day` (default today in the profile timezone), `minMinutes?`, `include`
(`notes` default; `transcript` adds the full text). Outputs: `meetings[]` (`title`, `start`, `end`, `attendees`, `notes`,
`transcript?`, `link`, `source`) and `count`. Two presets ship on the generic OAuth connections (PR 10; sign-in is PR 10b):
`granola` (Granola) and `wispr-flow` (Wispr Flow), the two MCP-hosted note apps from D8. Each preset maps its app's
meeting and notes data to the one shape above behind `IMeetingSource`, so a third note app is one class. Which API each
app exposes (MCP tools or REST) is checked when the preset is built, with a probe like `scripts/probe-linear-projects.sh`.
Notes and transcripts are untrusted and fenced wherever they reach a prompt. This PR lands after PR 10b.
Rollback: revert.

## Code changes
| path:line | change |
|---|---|
| src/Sindri/Aw.Sindri.Core/Graph/Blocks/MeetingsBlock.cs, MeetingsConfig.cs, IMeetingSource.cs (new) | block `meetings`, picker label Meetings, group Data; the validator checks `connection` is a meeting-notes connection |
| src/Sindri/Aw.Sindri.Infrastructure/Connections/Meetings/GranolaMeetingSource.cs, WisprFlowMeetingSource.cs (new) | map each app's meetings and notes to the shared record; host allowlist from the preset; text marked untrusted |
| ConnectionPresets (PR 10) `granola`, `wispr-flow` | preset rows: endpoint, scopes, host group |
| scripts/probe-meeting-apps.sh + scripts/tests/probe-meeting-apps.test.sh (new) | read-only probe that records which API (MCP tools or REST) each app exposes; run before the sources are written |
| tests/Aw.Sindri.Testing/FakeProvider.cs | fake Granola and Wispr Flow endpoints with controllable meeting lists |
| docs/workflows/blocks/meetings.md, docs/sindri/connections.md | example spec, preset notes |

## Graph context
- Depends on PR 10 (connections, OAuth presets) and PR 10b (sign-in); the block is a Data block like News.
- Notes and transcripts can hold anything a meeting said, so they are fenced like every other connection text.

## Tests
- MeetingsBlockTests.Today_is_resolved_in_the_profile_timezone_and_minMinutes_drops_short_meetings: fake clock.
- GranolaMeetingSourceTests.A_fixture_response_maps_to_meetings_with_notes_and_a_link: fixture JSON.
- WisprFlowMeetingSourceTests.A_fixture_response_maps_to_meetings_and_transcript_only_when_asked: `include: transcript`.
- MeetingsBlockTests.Notes_are_fenced_as_untrusted_when_they_reach_a_prompt: fence markers present.
- ProbeMeetingAppsTests.The_probe_prints_one_line_per_app_and_never_the_token: bash suite.
