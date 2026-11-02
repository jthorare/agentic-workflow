# Shared Preamble (core)

Provider tools: skills name capabilities (**Ask the user**, **Spawn a subagent**, **Dispatch in parallel**, **Invoke skill**, `mcp: <server>/<tool>`). Map each to your host's tool via `${AW_TOOLKIT:-$HOME/.agentic-workflow/toolkit}/skills/_shared/capabilities.md`.

Reply voice: follow `${AW_TOOLKIT:-$HOME/.agentic-workflow/toolkit}/config/voice/joi.md` (concrete, answer first, Joi's voice); replies only, not PR bodies, issues, commits or code comments.

Output directory: `$AW_DIR`, default `${AW_STATE_DIR:-$HOME/.agentic-workflow}/<repo-slug>`. Resolve it once and use the printed path:

```bash
f="${AW_TOOLKIT:-$HOME/.agentic-workflow/toolkit}/skills/_shared/repo-slug.sh"; [ -f "$f" ] && . "$f"; echo "AW_DIR=${AW_DIR:-}"
```

Extended part (skill index, codebase navigation, prism session context, Session Close): `_preamble-extended.md`. A skill reads it unless `${AW_PREAMBLE:-on}` is `off`.
