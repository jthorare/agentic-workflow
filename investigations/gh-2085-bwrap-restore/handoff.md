# Handoff: gh-2085-bwrap-restore

status: diagnosed
report: /home/thor/repos/aw-wt/api-2085/investigations/gh-2085-bwrap-restore/handoff.md
boundary: src/Sindri/Aw.Sindri.Core/Sandbox/Plan (cause: PlanMounts.cs `_homeDefaults`; error site: dotnet inside bwrap)
repro: /tmp/claude-1000/rc2085/run.sh (hand-built bwrap argv mirroring BubblewrapArgv + PlanMounts + PlanEnvironment; `heavy --light /tmp/claude-1000/rc2085/run.sh` -> exit=1)

## Root Cause
Not the SDK, and not /tmp. Runtime 10.0.14 (installed 2026-11-05 08:34 beside 10.0.12 under ~/.dotnet/shared/Microsoft.NETCore.App, shipped with SDK 10.0.403) creates the named mutex 'NuGet-Migrations' by first doing `mkdir $HOME/.dotnet/corefx/Runtime` (strace: `mkdir("/home/thor/.dotnet/corefx/Runtime", 0700) = -1 EROFS`). Inside the sandbox `$HOME/.dotnet` is a read-only bind (PlanMounts.cs `_homeDefaults` ".dotnet" ReadOnly), so the mkdir fails and Mutex.CreateMutexCore throws IOException. Runtime 10.0.12 does not need that directory, which is why it passed on 2026-11-04 and why nothing in the sandbox plan changed. `~/.dotnet/corefx` exists on the host (only `cryptography`), but no `Runtime` subdir.

## Hypotheses

| # | Hypothesis | Cause-site files | Likelihood | Result |
|---|-----------|------------------|------------|--------|
| 1 | New SDK/runtime makes the mutex current-user-only and its shm dir under /tmp/.dotnet/shm fails (perms/uid) | PlanMounts.cs, BubblewrapArgv.cs | High | ruled-out |
| 2 | /tmp or TMPDIR read-only or missing | PlanEnvironment.cs, PlanMounts.cs | Medium | ruled-out |
| 3 | Runtime 10.0.14 creates $HOME/.dotnet/corefx/Runtime for the NuGet-Migrations mutex, and the sandbox's read-only `.dotnet` home default mount makes that mkdir fail with EROFS | src/Sindri/Aw.Sindri.Core/Sandbox/Plan/PlanMounts.cs | High | confirmed |
| 4 | SDK 10.0.403 (not the runtime) is the difference | global.json | Medium | ruled-out |

Row notes:
- 1: the strace shows no /tmp/.dotnet access at all; pre-creating /tmp/.dotnet/shm 0777 inside the sandbox still exit=1. Right idea (a per-user dir for the mutex), wrong place: it is $HOME/.dotnet/corefx/Runtime (row 3).
- 2: /tmp is a writable bind of the run-area tmp; /dev/shm is a writable tmpfs (touch ok).
- 3: strace's last syscall before the throw is mkdir("/home/thor/.dotnet/corefx/Runtime", 0700) = -1 EROFS; a writable bind or a 0700 tmpfs over $HOME/.dotnet/corefx -> exit=0; `--fx-version 10.0.12` -> exit=0, `--fx-version 10.0.14` -> exit=1. DOTNET_CLI_HOME is irrelevant.
- 4: pinning SDK 10.0.401 (rollForward disable) still exit=1 because the runtime stays 10.0.14.


## Experiments (all under `heavy --light`, foreground, same bwrap argv, one change each)
- Baseline: restore of Fixture.csproj -> `exit=1`, NuGet-Migrations IOException.
- A. global.json pins SDK 10.0.401: exit=1.
- B. pre-create /tmp/.dotnet/shm (0777) in the sandbox: exit=1.
- strace -f: last syscall before the throw is `mkdir("/home/thor/.dotnet/corefx/Runtime", 0700) = -1 EROFS`.
- E2. `dotnet --fx-version 10.0.12 <sdk 10.0.403>/dotnet.dll restore ...`: exit=0 (SDK 403 unchanged).
- E3. `--fx-version 10.0.14`: exit=1.
- E1. only change: `--bind <scratch>/corefx $HOME/.dotnet/corefx`: exit=0 (Runtime dir appears in scratch).
- E4. only change: `--perms 0700 --tmpfs $HOME/.dotnet/corefx`: exit=0.
- Control, outside bwrap: exit 0.

## Ruled Out
- SDK version (403 vs 401): see A / E2.
- /tmp shm dir, perms, uid mapping: see B and the strace.

## Suggested Fix
Smallest change: give the session a writable `$HOME/.dotnet/corefx` under the read-only `.dotnet` mount, i.e. one more plan mount after the `.dotnet` default, e.g. `new PlanMount(c.Home + "/.dotnet/corefx", PlanMountKind.Tmpfs, Size: ..., Reason: "the runtime's named-mutex directory")` in PlanMounts.cs (the bubblewrap form is `--perms 0700 --tmpfs <home>/.dotnet/corefx`, proven E4). It must come after the `.dotnet` ReadOnly entry, and the host dir must exist (it does; bwrap cannot create a mountpoint in a ro bind), so if absent on a box it needs the `Optional`/create-on-host handling or the mutex dir moved with an env var. Also update the Seatbelt plan equivalently if it treats `.dotnet` read-only (macOS runtime likely uses the same path). Golden files for the bubblewrap argv (tests/Aw.Sindri.Tests/Sandbox/Bubblewrap/Golden) and BubblewrapArgvTests will change; per the team rules those need the ApiGoldens/Sandbox unit lane run in the lead's gate.
Open item for the fixer: whether the dir should be a tmpfs (per launch, as proven) or a bind to a shared dir (named mutexes shared with the owner's builds is not wanted); tmpfs is the safer pick.
