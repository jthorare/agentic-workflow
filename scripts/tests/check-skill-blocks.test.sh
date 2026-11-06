#!/usr/bin/env bash
# Tests for scripts/check-skill-blocks.mjs (the Sindri block manifests, skill-flows spec §3.1 "Checks"): the repo passes, a valid
# manifest passes, and each kind of drift is refused with its reason.
set -euo pipefail
. "$(dirname "${BASH_SOURCE[0]}")/../../scripts/lib/test-grep.sh"

REPO="$(cd "$(dirname "$0")/../.." && pwd)"
CHECK="$REPO/scripts/check-skill-blocks.mjs"
PASS=0
FAIL=0
TMPROOT="$(mktemp -d "${TMPDIR:-/tmp}/check-skill-blocks-test.XXXXXX")"
trap 'rm -rf "$TMPROOT"' EXIT

ok()   { PASS=$((PASS + 1)); echo "  ok   $1"; }
fail() { FAIL=$((FAIL + 1)); echo "  FAIL $1"; }

# Ajv and yaml are dev dependencies of scripts/ (scripts/package.json); installed once under a lock, since suites run in parallel.
# shellcheck source=scripts/lib/ensure-scripts-deps.sh
. "$REPO/scripts/lib/ensure-scripts-deps.sh"
ensure_scripts_deps "$REPO/scripts" || exit 1

run() { set +e; OUT="$(node "$CHECK" --root "$1" 2>&1)"; RC=$?; set -e; }
expect_rc() { if [ "$RC" -eq "$2" ]; then ok "$1 (exit $RC)"; else fail "$1: exit $RC, want $2"; printf '%s\n' "$OUT" | sed 's/^/      | /'; fi; }
expect_out() { if printf '%s\n' "$OUT" | has -F -- "$2"; then ok "$1"; else fail "$1: output lacks '$2'"; printf '%s\n' "$OUT" | sed 's/^/      | /'; fi; }

# The checker finds Ajv and yaml through its own location (scripts/); --root only moves skills/ and schemas/.
tree() {
  local d="$TMPROOT/$1"
  mkdir -p "$d/schemas" "$d/skills"
  cp "$REPO/schemas/skill-block.schema.json" "$d/schemas/"
  echo "$d"
}

# skill ROOT NAME [AWRESULT_JSON]: skills/NAME/SKILL.md with a Result block holding that aw-result example.
skill() {
  local d="$1/skills/$2"
  mkdir -p "$d"
  local json="${3:-{\"skill\":\"$2\",\"verdict\":\"PASS\",\"feature_dir\":\"/x/plans/p\",\"artifacts\":[\"/x/plans/p/report.md\"],\"summary\":\"s\"\}}"
  printf -- '---\nname: %s\n---\n\nBody.\n\n## Result block\n\n```aw-result\n%s\n```\n\n## Next steps\n\n- none\n' "$2" "$json" > "$d/SKILL.md"
}

# valid ROOT SKILL BLOCK_ID: a valid manifest for that skill.
valid() {
  cat > "$1/skills/$2/block.yaml" <<YAML
schema: 1
blocks:
  - id: $3
    sentence: Reviews a plan.
    inputs:
      plan: { type: path, label: Plan folder, required: true }
      mode: { type: text, label: Review mode, required: false, values: [mvp, growth] }
    args: ["{plan}", { flag: --mode, input: mode }]
    outputs:
      report:     { type: path, from: "artifacts[report.md]" }
      featureDir: { type: path, from: feature_dir }
    outcomes:
      from: verdict
      map: { PASS: pass, NEEDS_WORK: needsWork }
      success: pass
    pushes: false
    maxDuration: 3h
YAML
}

echo "repo"
run "$REPO"; expect_rc "the repo's manifests pass" 0; expect_out "prints ok" "check-skill-blocks: ok"

echo "valid"
T="$(tree good)"; skill "$T" alpha; valid "$T" alpha alpha
run "$T"; expect_rc "a valid manifest passes" 0
T="$(tree none)"; run "$T"; expect_rc "no manifests at all passes" 0

echo "refusals"
T="$(tree badyaml)"; skill "$T" alpha; printf 'schema: 1\nblocks: [oops\n' > "$T/skills/alpha/block.yaml"
run "$T"; expect_rc "YAML that does not parse fails" 1; expect_out "names the file" "skills/alpha/block.yaml: not valid YAML"

T="$(tree schema)"; skill "$T" alpha; valid "$T" alpha alpha; sed -i '/sentence:/d' "$T/skills/alpha/block.yaml"
run "$T"; expect_rc "a manifest the schema refuses fails" 1; expect_out "names the missing field" "schema: /blocks/0 must have required property 'sentence'"

T="$(tree comma)"; skill "$T" alpha; valid "$T" alpha alpha; sed -i 's/label: Plan folder,/label: Plan folder (a path, or a link),/' "$T/skills/alpha/block.yaml"
run "$T"; expect_rc "an unquoted comma in a flow-map label fails" 1; expect_out "names the extra property" "must NOT have additional properties"

T="$(tree dupid)"; skill "$T" alpha; valid "$T" alpha same; skill "$T" beta; valid "$T" beta same
run "$T"; expect_rc "a block id used twice fails" 1; expect_out "names the first file" "id also used in skills/alpha/block.yaml"

T="$(tree slot)"; skill "$T" alpha; valid "$T" alpha alpha; sed -i 's/"{plan}"/"{plan}", "{nope}"/' "$T/skills/alpha/block.yaml"
run "$T"; expect_rc "an args slot that is no input fails" 1; expect_out "names the slot" "args names 'nope', which is not a declared input"

T="$(tree flag)"; skill "$T" alpha; valid "$T" alpha alpha; sed -i 's/input: mode }/input: modes }/' "$T/skills/alpha/block.yaml"
run "$T"; expect_rc "a flag item naming no input fails" 1; expect_out "names the input" "args names 'modes'"

T="$(tree required)"; skill "$T" alpha; valid "$T" alpha alpha; sed -i 's/args: \["{plan}", /args: [/' "$T/skills/alpha/block.yaml"
run "$T"; expect_rc "a required input missing from args fails" 1; expect_out "names the input" "required input 'plan' does not appear in args"

T="$(tree success)"; skill "$T" alpha; valid "$T" alpha alpha; sed -i 's/success: pass/success: done/' "$T/skills/alpha/block.yaml"
run "$T"; expect_rc "a success outcome that is not mapped fails" 1; expect_out "names it" "success 'done' is not one of the mapped outcomes"

T="$(tree values-path)"; skill "$T" alpha; valid "$T" alpha alpha; sed -i 's/mode: { type: text,/mode: { type: path,/' "$T/skills/alpha/block.yaml"
run "$T"; expect_rc "values on a path input fail" 1; expect_out "names the input" "input 'mode' has values but is a path input"

T="$(tree values-empty)"; skill "$T" alpha; valid "$T" alpha alpha; sed -i 's/values: \[mvp, growth\]/values: []/' "$T/skills/alpha/block.yaml"
run "$T"; expect_rc "an empty values list fails" 1; expect_out "names the rule" "/blocks/0/inputs/mode/values must NOT have fewer than 1 items"

T="$(tree from-key)"; skill "$T" alpha; valid "$T" alpha alpha; sed -i 's/from: feature_dir/from: plan_dir/' "$T/skills/alpha/block.yaml"
run "$T"; expect_rc "an output reading a key the aw-result block lacks fails" 1; expect_out "names the key" "output 'featureDir' reads 'plan_dir', which is not a key of the aw-result block"

T="$(tree from-artifact)"; skill "$T" alpha; valid "$T" alpha alpha; sed -i 's/artifacts\[report.md\]/artifacts[other.md]/' "$T/skills/alpha/block.yaml"
run "$T"; expect_rc "an artifact file name the example lacks fails" 1; expect_out "names the file and what exists" "reads artifacts[other.md], which no path in the aw-result example's artifacts ends in (it has: report.md)"

T="$(tree outcome-from)"; skill "$T" alpha; valid "$T" alpha alpha; sed -i 's/from: verdict/from: status/' "$T/skills/alpha/block.yaml"
run "$T"; expect_rc "outcomes.from naming a missing key fails" 1; expect_out "names the key" "outcomes.from 'status' is not a key of the aw-result block"

T="$(tree long-fact)"; skill "$T" alpha; valid "$T" alpha this-block-id-is-written-out-and-it-is-rather-long-indeed; sed -i 's/featureDir:/featureDirectoryOfTheRun:/' "$T/skills/alpha/block.yaml"
run "$T"; expect_rc "a fact key over 64 characters fails" 1; expect_out "names the key" "fact key 'fact:thisBlockIdIsWrittenOutAndItIsRatherLongIndeedFeatureDirectoryOfTheRun' is"

T="$(tree dup-fact)"; skill "$T" alpha; valid "$T" alpha a-b; sed -i 's/featureDir:/cD:/' "$T/skills/alpha/block.yaml"
skill "$T" beta; valid "$T" beta a; sed -i 's/featureDir:/bCD:/' "$T/skills/beta/block.yaml"
run "$T"; expect_rc "two blocks deriving one fact key fail" 1; expect_out "names the key" "fact key 'fact:aBCD' is also derived in"

T="$(tree no-skill-md)"; skill "$T" alpha; valid "$T" alpha alpha; rm "$T/skills/alpha/SKILL.md"
run "$T"; expect_rc "a manifest with no SKILL.md fails" 1; expect_out "names it" "no SKILL.md beside it"

T="$(tree no-result)"; skill "$T" alpha; valid "$T" alpha alpha; sed -i '/^```aw-result/,/^```$/d' "$T/skills/alpha/SKILL.md"
run "$T"; expect_rc "a Result block with no aw-result fence fails" 1; expect_out "names the count" "the Result block has 0 aw-result blocks"

T="$(tree two-result)"; skill "$T" alpha; valid "$T" alpha alpha
sed -i 's/^## Next steps$/```aw-result\n{"skill":"alpha"}\n```\n\n## Next steps/' "$T/skills/alpha/SKILL.md"
run "$T"; expect_rc "two aw-result fences fail" 1; expect_out "names the count" "the Result block has 2 aw-result blocks"

T="$(tree bad-json)"; skill "$T" alpha '{ "skill": "alpha", '; valid "$T" alpha alpha
run "$T"; expect_rc "an aw-result example that is not JSON fails" 1; expect_out "names the problem" "the aw-result block is not valid JSON"

T="$(tree wrong-skill)"; skill "$T" alpha '{"skill":"beta","verdict":"PASS","feature_dir":"/x","artifacts":["/x/report.md"],"summary":"s"}'; valid "$T" alpha alpha
run "$T"; expect_rc "an aw-result example for another skill fails" 1; expect_out "names both" "the aw-result example says skill 'beta', want 'alpha'"

echo
echo "passed: $PASS, failed: $FAIL"
[ "$FAIL" -eq 0 ]
