---
type: regex
pattern: '"command":"(?:[^"\\]|\\.)*?toollib\.example\.test/health(?:(?:[^"\\]|\\.)*?\bapi/|[\s\S]*?"command":"(?=(?:[^"\\]|\\.)*?\bcurl\b)(?:[^"\\]|\\.)*?\bapi/)'
target: trace
---

Smoke tests run after the health check: a Bash command hits the health URL and then, in the same command or a later one, a curl hits an `api/` smoke URL. Same-command runs (`curl .../health; for u in api/ping ...; do curl .../$u; done`) and split runs both count; a tool_order check can't see the order inside one command.
