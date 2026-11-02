# intake family

One prompt per member (officeHours, withInterview, enhancePrompt). Each grader is a `regex` on the argument of the FIRST Skill
tool call only; later calls do not count (spec 3.1a). Run with `--ablation none` whenever a member's description changes.

Draft: the `source: first_tool_call` / `tool: Skill` keys are the intended meaning; the lead adapts them to the final grader
schema when the engine lands. `withInterview` has `disable-model-invocation: true`, so its prompt cannot fire it through the Skill tool.
