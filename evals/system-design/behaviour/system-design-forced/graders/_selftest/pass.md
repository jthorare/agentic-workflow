<!-- selftest: a saved run that passes every grader of this case. The reply is the text under the Reply heading; the Trace heading lists the tool calls in order (what tool_used / tool_order graders read) and the files the run left in the workspace (what file_exists reads). -->

## Trace

```jsonl
{"tool":"Skill","input":{"skill":"system-design"}}
{"tool":"Read","input":{"file_path":"/work/docs/ticket-FERRY-31.md"}}
{"tool":"Write","input":{"file_path":"/work/.aw-state/work/system-design/booking-history/status.md"}}
{"tool":"Bash","input":{"command":"python3 -I \"$S/scripts/tokens_to_css.py\" design-tokens.json \"$SD/doc-style.css\""}}
{"tool":"Write","input":{"file_path":"/work/.aw-state/work/system-design/booking-history/tdd.html"}}
{"tool":"Write","input":{"file_path":"/work/.aw-state/work/system-design/booking-history/product-design.src.html"}}
{"tool":"Bash","input":{"command":"python3 -I \"$S/scripts/write_packet.py\" \"$SD\" --slug booking-history --title \"Booking history on the lookup page\""}}
```

Files left: `.aw-state/work/system-design/booking-history/tdd.html`, `.aw-state/work/system-design/booking-history/product-design.src.html`, `.aw-state/work/system-design/booking-history/status.md`

## Reply

The packet for FERRY-31 is written under `.aw-state/work/system-design/booking-history/`.

Decisions recorded in `status.md`: D1, the history must show within a hard 2-second limit on the desk PCs; D2, only the last 12 months.

Line from the engineering page that covers D1: "Latency budget: the lookup returns the booking and its history in under 2 seconds on a desk PC, no exceptions (D1); the history query is capped at 12 months (D2)."

Not done: tldraw is not installed here, so the diagrams step is blocked and no `.svg` was drawn. Playwright is missing too, so page verification was skipped. The mockups and the tracker were skipped as you asked. Counts are unknown: AGENTS.md documents no read access, so the status doc says so.

Files I wrote: tdd.html, product-design.src.html, status.md. write_packet.py failed on the read-only filesystem, so packet.json was not written.
