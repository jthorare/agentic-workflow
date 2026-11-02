# Personas: one video per audience

| Cut | Question | Story | Beats |
| --- | --- | --- | --- |
| Engineering (default) | What do I build, in what order, and what breaks? | the design doc walked in order: the model (old against new, how rows map), the write and read paths, the rollout and its gates, the work breakdown with dependencies | tables and keys; the backfill mapping; writers, dual write, readers, the serving flag, the mismatch log; the states and the check between them; each PR with what it changes |
| Product (secondary) | What will users see and decide? | one thing to set, a rule per workflow, what changes for the people using it | today's separate settings; the one setting; each workflow reading it; the edit that moves them; what stays the same |
| Design (optional) | Which screens change? | the surfaces list | only when a design packet names the surfaces |

Rules: merge cuts that would draw the same board; drop an audience with nothing to see. The product cut never names tables, flags or services; the engineering cut never explains what the product is for. Both use the same worked example so the numbers agree.
