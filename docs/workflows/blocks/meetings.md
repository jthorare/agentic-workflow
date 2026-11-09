# `meetings` block

Reads one day's meetings from a meeting-notes connection: Granola (`granola:<label>`) or Wispr Flow (`wispr-flow:<label>`).
Picker label Meetings, group Data. Make the connection first (`sindri connection add granola:work --preset granola --client-id ...`,
[connections.md](../../sindri/connections.md)).

| Config | Meaning |
|---|---|
| `connection` | A connection made from a meeting-notes preset. A `linear:work` connection fails the check on save |
| `day` | `today` (default) or a date such as `2026-11-09`. "Today" is the day in the profile timezone, not in UTC |
| `minMinutes` | Drop meetings shorter than this many minutes. Default 0 keeps all |
| `include` | `notes` (default), or `transcript` to add the full text |

Outputs: `meetings[]` (`title`, `start`, `end`, `attendees`, `notes`, `transcript?`, `link`, `source`) and `count`.
`start` and `end` carry the profile timezone's offset, for example `2026-11-09T09:00:00-08:00`. The list is in start order.

Notes and transcripts are what people said in a meeting: untrusted. The block wraps both in the untrusted fence, so they stay
fenced wherever a later node, such as a `prompt`, uses them. `title` and `attendees` are not fenced.

```json
{"connection":"granola:work","day":"today","minMinutes":10,"include":"notes"}
```

A third note app is one class behind `IMeetingSource`; its preset key is the connection name's service part.

Status: the Granola and Wispr Flow mappings are not built until `scripts/probe-meeting-apps.sh` has run with real accounts. Until
then a `meetings` node fails with a message saying so.
