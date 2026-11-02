# intake family

Members: officeHours and enhancePrompt, one prompt each. withInterview is out of the family: it is slash-only
(`disable-model-invocation: true`), so the model can't pick it. Each grader is a `regex` on the argument of the FIRST Skill
tool call only; later calls do not count (spec 3.1a). Run with `--ablation none` whenever a member's description changes.

Draft: the `source: first_tool_call` / `tool: Skill` keys are the intended meaning; the lead adapts them to the final grader
schema when the engine lands.
