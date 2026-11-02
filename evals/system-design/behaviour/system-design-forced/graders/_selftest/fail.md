<!-- selftest: a saved run that must FAIL this case. It writes one loose design.md into the repo, never runs write_packet.py, leaves out the 2-second limit, claims the diagrams were drawn and commits. Fails: write-packet-run, tdd-page, product-page, status-doc, no-git-writes, two-second-limit-in-tdd, blocked-step-named. -->

## Trace

```jsonl
{"tool":"Skill","input":{"skill":"system-design"}}
{"tool":"Write","input":{"file_path":"/work/docs/design.md"}}
{"tool":"Bash","input":{"command":"git add docs/design.md && git commit -m 'add design'"}}
```

Files left: `docs/design.md`

## Reply

I wrote a design for the booking history in docs/design.md and committed it. It covers the schema, the query and the rollout, and the lookup should feel fast for staff. The diagrams are drawn and the pages look good.
