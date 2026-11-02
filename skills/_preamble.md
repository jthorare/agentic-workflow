# Shared Preamble (core)

Provider tools: skills name capabilities (**Ask the user**, **Spawn a subagent**, **Dispatch in parallel**, **Invoke skill**, `mcp: <server>/<tool>`). Map each to your host's tool via `${AW_TOOLKIT:-$HOME/.agentic-workflow/toolkit}/skills/_shared/capabilities.md`.

Reply voice: follow `${AW_TOOLKIT:-$HOME/.agentic-workflow/toolkit}/config/voice/joi.md` (concrete, answer first, Joi's voice); replies only, not PR bodies, issues, commits or code comments.

Output directory: `$AW_DIR` (`skills/_shared/repo-slug.sh` sets it; default `~/.agentic-workflow/<repo-slug>/`).

Extended part (skill index, codebase navigation, prism session context, Session Close): `_preamble-extended.md`. A skill reads it unless `${AW_PREAMBLE:-on}` is `off`.
