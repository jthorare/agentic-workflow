✗ Dashboard: nothing answered on http://localhost:7391/ (curl exit 7)
✗ MCP surface: prism-mcp/session_health_check is not available in this session
✗ Knowledge base: could not load context, so the entry count is unknown
✗ Version: unknown (no health response, no mcp registration found)

Fix: re-run `./setup.sh` to register prism-mcp, check PRISM_DASHBOARD_PORT (you passed 7391), then retry.

```aw-result
{"skill": "prismStatus", "artifacts": [], "summary": "prism-mcp is down on port 7391", "dashboard": "fail", "mcp": "fail", "kb": "fail", "version": "unknown"}
```
