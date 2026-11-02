#!/usr/bin/env bash
# scripts/scaffold/e2e.sh — the generators' end-to-end test (docs/csharp/scaffolds/new-package.md, "Done criteria"):
# scaffolds (a) a small CLI package declared in deps.yaml and (b) a full http package that isn't, each in its own
# temporary git worktree of HEAD, and lets the generator run its own checks against the real root files
# (restores, RS0016, build -warnaserror and the unit lane of the package's test project and the architecture tests, format
# limited to the files it wrote, check-deps; one whole-solution build per tree at the end). Then, by hand:
# (a) makes its first slice green and adds a second slice with /new-slice (exactly one new red test), then
# NativeAOT-publishes its App for the runner's RID (no IL2xxx/IL3xxx warning) and runs the native binary (usage exit 2
# and the AW-TELEMETRY-001 line for an unknown AW_LOG_LEVEL); (b) makes its first slice green, adds a second with a new
# Guid id and a new port (Core's and Features' PublicAPI lines come from the build's RS0016 diagnostics), makes that
# green and adds a third reusing the port (which must leave the port, its fake and its contract base alone), makes that
# green and adds a fourth with --slice-only (only its own files may change; the generator's checks leave exactly its
# test red), then runs its integration lane (loopback binding, id route binding, Kernel telemetry as the only logger) green. The worktrees
# are removed afterwards.
#
# Needs the NativeAOT toolchain for the AOT step: the Xcode command-line tools on macOS, clang and zlib on Linux.
# Usage: scripts/scaffold/e2e.sh            (from the repo root; needs the .NET SDK from global.json and node)
# Exit:  0 both packages behaved; 1 a check failed (its output is printed); 2 usage or setup error.
#
# The generators restore new projects, which have no lock file yet: under CI=true (RestoreLockedMode, the AW0001
# lock-file guard) that is refused by design, so the generator steps run with CI unset. CI's own locked restore
# and package checks cover the committed tree, not these throwaway packages.
set -euo pipefail

ROOT="$(git rev-parse --show-toplevel)"
cd "$ROOT"
TMP="$(mktemp -d "${TMPDIR:-/tmp}/aw-scaffold-e2e.XXXXXX")"
# At most AW_HEAVY_SLOTS heavy jobs at a time (default 2; config/lib/locks.sh): take a slot of the box-wide lock for
# the whole run unless a caller holds one, and tell the generators it is held.
LOCK=""
if [ "${AW_HEAVY_LOCK_HELD:-}" != "1" ]; then
  # shellcheck source=../../config/lib/locks.sh
  source "$ROOT/config/lib/locks.sh"
  BASE="${AW_HEAVY_JOB_LOCK:-${AW_STATE_DIR:-$HOME/.agentic-workflow}/locks/heavy-job.lock}"
  mkdir -p "$(dirname "$BASE")"
  LOCK="$(acquire_any_lock "$BASE" "${AW_HEAVY_SLOTS:-2}" 5400)" || { echo "heavy-job lock busy (every slot): $BASE" >&2; exit 2; }
  echo "$$ $(date -u +%FT%TZ) scripts/scaffold/e2e.sh" > "$LOCK/owner"
fi
export AW_HEAVY_LOCK_HELD=1
cleanup() {
  local dir
  for dir in "$TMP/small" "$TMP/http"; do
    if [ -d "$dir" ]; then git -C "$ROOT" worktree remove --force "$dir" >/dev/null 2>&1 || true; fi
  done
  rm -rf "$TMP"
  git -C "$ROOT" worktree prune
  if [ -n "$LOCK" ]; then rm -f "$LOCK/owner"; release_lock "$LOCK"; fi
}
trap cleanup EXIT

step() { printf '\n== %s\n' "$*"; }
fail() { printf 'FAIL: %s\n' "$*" >&2; exit 1; }

# tree <name> — a throwaway worktree of HEAD at $TMP/<name>; cleanup removes it.
tree() { git worktree add --quiet --detach "$TMP/$1" HEAD; }

# The first package deps.yaml declares (tier 1-3) that has no src/ directory yet, so the declared-node path
# (NP-DEPS-MISMATCH checks, a planned edge flipping to current) is exercised while unported packages remain. Once every
# declared package is ported, (a) falls back to an undeclared small probe (the node-adding path (b) also covers).
declared="$(node --input-type=module -e '
  import { parseYaml } from "./scripts/deps/yaml.mjs";
  import fs from "node:fs";
  const doc = parseYaml(fs.readFileSync("deps.yaml", "utf8"));
  const n = doc.packages.find((p) => p.kind === "package" && p.tier >= 1 && !p.test && !fs.existsSync(p.src) && /^src\/[A-Z][A-Za-z0-9]+$/.test(p.src));
  if (!n) process.exit(3);
  console.log(`${n.id} ${n.tier}`);
')" || declared=""
if [ -n "$declared" ]; then
  read -r SMALL SMALL_TIER <<<"$declared"
  SMALL_NOTE=()
  SMALL_KIND="declared in deps.yaml"
else
  SMALL="Scaffoldsmall"
  SMALL_TIER=1
  SMALL_NOTE=(--note "scaffold e2e probe")
  SMALL_KIND="no declared package left unported: an undeclared probe"
fi
HTTP="Scaffoldprobe"

gen() { env -u CI node "$@"; }

# green <tree> <features dir> <tests dir> <Slice> <behavior> — make a scaffolded red slice pass, as its implementer would.
green() {
  perl -0pi -e "s/=> throw new NotSupportedException\\(\"$4: make $4Tests pass\"\\);/=> ValueTask.FromResult<Result<$4Response>>(new $4Response());/" "$1/$2/$4/$4Service.cs"
  perl -0pi -e "s/\\n        Assert\\.Fail\\(\"red: assert $5\"\\);//" "$1/$3/$4/$4Tests.cs"
}

# ---- (a) small CLI package, declared ----
tree small
A="$TMP/small"
step "(a) /new-package: $SMALL, small, cli, tier $SMALL_TIER ($SMALL_KIND)"
(cd "$A" && gen scripts/scaffold/new-package.mjs --name "$SMALL" --tier "$SMALL_TIER" --error-prefix E2ESMALL ${SMALL_NOTE[@]+"${SMALL_NOTE[@]}"} \
  --shape small --host cli --first-slice CheckThing --behavior reports_nothing_for_an_empty_tree) || fail "(a) new-package"
(cd "$A" && git status --porcelain --untracked-files=all | grep -q "src/$SMALL/") || fail "(a) wrote no src/$SMALL"

step "(a) make CheckThing green by hand, then /new-slice ListThings with a typed id and a new port"
green "$A" "src/$SMALL/Aw.$SMALL.App/Features" "tests/Aw.$SMALL.Tests" CheckThing reports_nothing_for_an_empty_tree
(cd "$A" && gen scripts/scaffold/new-slice.mjs --package "$SMALL" --slice ListThings --behavior lists_nothing_for_an_empty_tree \
  --surface cli --command list --ids ThingId:string --ports IThingStore) || fail "(a) new-slice"

step "(a) NativeAOT publish of the generated cli App (telemetry included): no trim or AOT warning, native run"
# PublishAot under a RID adds the host's ILCompiler packs to the lock files, which only this throwaway worktree sees.
rid="$(dotnet --info | awk '/^ *RID:/ {print $2; exit}')"
(cd "$A" && env -u CI dotnet publish "src/$SMALL/Aw.$SMALL.App/Aw.$SMALL.App.csproj" -c Release -r "$rid" \
  -p:RestoreLockedMode=false -o "$TMP/small-aot" >"$TMP/small-aot.log" 2>&1) || { tail -20 "$TMP/small-aot.log" >&2; fail "(a) AOT publish"; }
if grep -E 'IL[23][0-9]{3}' "$TMP/small-aot.log" >&2; then fail "(a) AOT publish raised trim or AOT warnings"; fi
bin="$TMP/small-aot/$(printf '%s' "$SMALL" | tr '[:upper:]' '[:lower:]')"
rc=0; AW_LOG_LEVEL=bogus "$bin" >/dev/null 2>"$TMP/small-aot.err" || rc=$?
[ "$rc" = 2 ] || fail "(a) native run with no command: exit $rc, expected 2 (usage)"
grep -q '"eventName":"AW-TELEMETRY-001"' "$TMP/small-aot.err" || fail "(a) native run: no AW-TELEMETRY-001 line for AW_LOG_LEVEL=bogus"

# ---- (b) full http package, not declared ----
tree http
B="$TMP/http"
step "(b) /new-package: $HTTP, full, http, tier 1 (not in deps.yaml)"
(cd "$B" && gen scripts/scaffold/new-package.mjs --name "$HTTP" --tier 1 --error-prefix E2EHTTP --note "scaffold e2e probe" \
  --shape full --host http --first-slice StartItem --behavior starts_an_item --route "POST /items/{itemId}/start" \
  --ids ItemId:string,OwnerId:Guid) || fail "(b) new-package"
step "(b) make StartItem green, then /new-slice ListItems with a new Guid id and a new port (Core and Features PublicAPI)"
green "$B" "src/$HTTP/Aw.$HTTP.Features" "tests/Aw.$HTTP.Tests" StartItem starts_an_item
(cd "$B" && gen scripts/scaffold/new-slice.mjs --package "$HTTP" --slice ListItems --behavior lists_nothing_for_an_owner \
  --surface http --route "POST /items/{itemId}/list" --ids BatchId:Guid --ports IItemStore) || fail "(b) new-slice ListItems"
grep -qx "Aw.$HTTP.Core.Ports.IItemStore" "$B/src/$HTTP/Aw.$HTTP.Core/PublicAPI.Unshipped.txt" || fail "(b) IItemStore not declared in Core's PublicAPI"
grep -qx "Aw.$HTTP.Features.ListItems.ListItemsRequest" "$B/src/$HTTP/Aw.$HTTP.Features/PublicAPI.Unshipped.txt" || fail "(b) ListItemsRequest not declared in Features' PublicAPI"
step "(b) make ListItems green, then /new-slice CountItems reusing IItemStore: its port, fake and contract stay as they are"
green "$B" "src/$HTTP/Aw.$HTTP.Features" "tests/Aw.$HTTP.Tests" ListItems lists_nothing_for_an_owner
(cd "$B" && git add -A && git -c user.name=e2e -c user.email=e2e@localhost commit --quiet -m "e2e: ListItems green") || fail "(b) commit"
(cd "$B" && gen scripts/scaffold/new-slice.mjs --package "$HTTP" --slice CountItems --behavior counts_nothing \
  --surface http --route "POST /items/{itemId}/count" --ports IItemStore) || fail "(b) new-slice CountItems"
reused="$(cd "$B" && git status --porcelain -- "src/$HTTP/Aw.$HTTP.Core/Ports" "tests/Aw.$HTTP.Testing/InMemoryItemStore.cs" "tests/Aw.$HTTP.Testing/ItemStoreContract.cs")"
[ -z "$reused" ] || fail "(b) CountItems rewrote the existing port or its fake: $reused"
step "(b) make CountItems green, then /new-slice --slice-only GetShelf with a new string route id: only its own files change"
green "$B" "src/$HTTP/Aw.$HTTP.Features" "tests/Aw.$HTTP.Tests" CountItems counts_nothing
(cd "$B" && git add -A && git -c user.name=e2e -c user.email=e2e@localhost commit --quiet -m "e2e: CountItems green") || fail "(b) commit"
(cd "$B" && gen scripts/scaffold/new-slice.mjs --slice-only --package "$HTTP" --slice GetShelf --behavior gets_a_shelf \
  --surface http --route "GET /shelves/{shelfId}" --ids ShelfId:string) || fail "(b) new-slice --slice-only GetShelf"
changed="$(cd "$B" && git status --porcelain --untracked-files=all | cut -c4- | sort)"
expected="$(sort <<EOF
src/$HTTP/Aw.$HTTP.Core/Ids/ShelfId.cs
src/$HTTP/Aw.$HTTP.Core/PublicAPI.Unshipped.txt
src/$HTTP/Aw.$HTTP.Features/GetShelf/GetShelfRequest.cs
src/$HTTP/Aw.$HTTP.Features/GetShelf/GetShelfResponse.cs
src/$HTTP/Aw.$HTTP.Features/GetShelf/GetShelfService.cs
src/$HTTP/Aw.$HTTP.Features/GetShelf/GetShelfValidator.cs
src/$HTTP/Aw.$HTTP.Features/PublicAPI.Unshipped.txt
tests/Aw.$HTTP.Testing/A.cs
tests/Aw.$HTTP.Testing/GetShelfRequestBuilder.cs
tests/Aw.$HTTP.Tests/GetShelf/GetShelfTests.cs
tests/Aw.$HTTP.Tests/Ids/IdsJsonContext.cs
tests/Aw.$HTTP.Tests/Ids/ShelfIdHolder.cs
tests/Aw.$HTTP.Tests/Ids/ShelfIdRouteBindingTests.cs
tests/Aw.$HTTP.Tests/Ids/ShelfIdTests.cs
EOF
)"
[ "$changed" = "$expected" ] || fail "(b) --slice-only changed other files: $(diff <(echo "$expected") <(echo "$changed") | tr '\n' ' ')"
step "(b) integration lane: loopback binding and id route binding (GetShelf's ShelfId included)"
(cd "$B" && env -u CI dotnet test --project "tests/Aw.$HTTP.Tests" --filter-trait "Lane=integration") || fail "(b) integration lane"
step "(b) check-deps with the new node"
(cd "$B" && node scripts/check-deps.mjs) || fail "(b) check-deps"

# The generators check only what they wrote (the package's test project and the architecture tests build and run,
# format is limited to the files written); this is the one whole-solution build, so a generator change that breaks
# some other project is still caught.
step "(a) whole-solution build -warnaserror"
(cd "$A" && env -u CI dotnet build AgenticWorkflow.slnx -warnaserror) || fail "(a) whole-solution build"
step "(b) whole-solution build -warnaserror"
(cd "$B" && env -u CI dotnet build AgenticWorkflow.slnx -warnaserror) || fail "(b) whole-solution build"

step "both packages scaffolded, built and tested; the worktrees are removed on exit"
