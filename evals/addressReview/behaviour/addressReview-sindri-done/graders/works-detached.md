---
type: regex
pattern: 'git\s+checkout\s+--detach\s+(?:sindri/inst-1/1/work|\$AW_SINDRI_BRANCH)'
flags: i
---

It works detached at the PR branch tip (`git checkout --detach $AW_SINDRI_BRANCH`, listed under `Would run:`), not through `gh pr checkout`.
