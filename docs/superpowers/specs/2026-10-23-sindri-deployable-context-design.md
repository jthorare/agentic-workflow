# Sindri as a deployable unit: one Docker image, with Onyx's containers beside it in the same pod

> **Superseded by 2026-10-27** for everything about Onyx (`2026-10-27-sindri-owned-index-design.md`): Onyx is removed, so no Onyx containers run beside Sindri. Kept as history.

**Status:** design, revision 6 (the owner's answer q83 of 2026-10-23 applied: agents are not part of the pod, so everything that existed only for them is cut; §13 lists what changed). The owner said "Good to go" on 2026-10-23 (q83); no open question remains · **Date:** 2026-10-23 · **Asked by:** the owner, 2026-10-23 (relayed by
sindri-meta): "make Sindri + Onyx a deployable unit for an EKS pod that stands up both. The owner port-forwards to it to set it up
(profile approval, Onyx admin, connectors)", refined the same night: "the deliverable is a Dockerfile that wraps Sindri", then (q73) "Sindri and Onyx are one
deployable unit; Onyx's prepackaged images run beside Sindri; nothing privileged, no Docker-in-Docker", and (q83) "why are agents being pod contained that's over engineering. i just want sindri to be a docker image i can throw onto my aws infra". The full-map page asked for the same night is Part D of this spec
(`2026-10-23-sindri-context-whole-map-design.md`). · **Builds on:** the API surface spec
(`2026-10-10-sindri-api-surface-design.md` §6 local-only, §11 deferred auth), the Onyx managed spec
(`2026-10-16-sindri-onyx-managed-design.md`), the context retrieval spec, `docs/csharp/sindri.md` (home layout, lease, serve),
`docs/csharp/forge.md` (credentials), `binaries.lock`, `context/onyx.lock` · **Lanes:** context (image, chart, connect, Onyx); api `onyx attach` (K-O2); engine the repo freshness service (§C.2); ui Part D's page; ideas the follow-ups of §9 · **Next:** /specToProvenPR takes §9's phases to PRs.

**Keeping it app-agnostic.** Nothing here names a work app, company, cluster, registry or host. Every one of those is a value the
deployer gives (Helm values, the profile, Kubernetes secrets), and the examples use `example.com` and `registry.example.com`.

## 0. The short version

### For you

One Helm chart installs one pod that runs Sindri and Onyx (the search over your chat and tickets) from Docker images, on a Kubernetes
cluster such as an EKS cluster. You reach it from your laptop with `kubectl port-forward` and `kubectl exec`, which is also how you set it
up. In order:

1. Install the chart.
2. Terminal 1: `scripts/sindri-pod.sh connect` (opens the Sindri app on the pod; it holds the terminal).
3. Terminal 2: `scripts/sindri-pod.sh doctor`, until Onyx shows ready.
4. Terminal 1: `scripts/sindri-pod.sh connect --onyx` (your one Onyx sign-up).
5. Terminal 2: `scripts/sindri-pod.sh profile edit --no-approve` (allow your git host), `scripts/sindri-pod.sh repo add <name> <url>` for
   each repo, and `scripts/sindri-pod.sh approve` once.

The exact commands, with every flag, are in §C.3. Where coding agents run, and how they connect to Sindri, is not part of this spec
(B.4). If you only have a small node, Onyx stays off and you get code and docs only.

### For builders

Today Sindri is a desktop program: it binds to loopback only, trusts whoever holds the token file in its home, runs one copy per
person, and manages Onyx with Docker Compose on the same machine. In a pod, most of that stays. This spec turns it into:

1. **One image** (`Dockerfile` at the repo root, built by CI from the repo): Sindri's control plane (as `sindri-app` publishes it),
   the pinned `temporal` binary, the code-index tools, `git`, and nothing secret. It runs `sindri serve` in the foreground with all
   state on one volume. It has no Docker CLI and no daemon.
2. **Onyx beside it, in the same pod, from one chart.** Onyx's own prepackaged images run next to Sindri as ordinary containers.
   Nothing is privileged and nothing runs Docker. A script renders them from Onyx's pinned Compose file, so an Onyx upgrade is:
   bump the lock, re-render, `helm upgrade`. Sindri uses this Onyx as one it does not manage; one command, `sindri onyx attach`,
   does the account steps.
3. **No new door.** Sindri's API stays a loopback-only listener, byte for byte; there is no Service and nothing listens on the pod's
   address. The owner, through `kubectl port-forward` and `exec`, keeps every right they have today. Where agents run is out of scope (B.4).
4. **Always fresh**: a new Sindri service pulls each repo's default branch; the code index, the docs index and the map follow it
   as they already do; Onyx's connectors (set up once by the owner in Onyx's page) keep tickets and chat current; Sindri's existing
   sync and refresh services read them and link what they find into the map.
5. **The owner's steps** stay one command or one click each: `scripts/sindri-pod.sh connect` opens the app on the pod;
   `connect --onyx` also forwards Onyx's page for the one browser sign-up and runs `sindri onyx attach` in the pod, before the one
   approval; `profile edit` is the one way to change a profile key in the pod; approvals are the app's existing
   one-click-and-type confirmations (or `scripts/sindri-pod.sh approve` at a terminal).
6. **No model in Sindri v1.** Everything the map and the `context_*` tools do works without one; only labelling suggested links and the
   decisions pass need a model, and they stay off in the pod (K12).

The price of one unit: the pod holds all of Onyx, Sindri and the Ollama sidecar, so it must fit **one node**. Onyx's floor
alone is about 5.5 GiB of memory and its images are about 11 GB to pull. **Recommended node: 8 vCPU, 32 GiB of memory and 100 GiB of
node disk, plus 120 GiB of volumes the cluster creates for you** (the pod asks for 7 vCPU and 25 GiB; the floor is 8 vCPU, 28 GiB and
75 GiB of node disk, C.1). The spike measures the whole pod (K-S0). A node too small for Onyx means Onyx off.

## 1. Decisions

The owner answered K1, K2, K3 and K12 on 2026-10-23 (revision 2), q73 the same day (revision 3) and q83 the same day (revision 6);
those rows are **decided**. The rest keep their defaults, which builders follow if the owner says nothing. No phase waits for the owner: K-O1 still waits
for K-S0's report.

> **Your answers in plain words.**
>
> 1. **What you get. Decided (q73, q83).** Sindri as a Docker image you can run on your own Kubernetes cluster (an EKS cluster is the
>    target), installed by one chart. Onyx, the search over your chat and tickets, runs beside it in the same pod from its own ready-made
>    images. Nothing is privileged and nothing runs Docker inside the pod.
> 2. **Who can reach it. Decided: you.** Sindri can be reached only from inside its own container, the same as on your laptop, and you reach
>    it with `kubectl port-forward` and `kubectl exec`. Nothing is opened to the rest of the cluster.
> 3. **Where coding agents run is not decided here.** This spec does not run them and does not say how they reach Sindri. A caller that
>    reaches Sindri's loopback (you, through `kubectl`) uses it as today; opening it to other machines is a later spec.
> 4. **The machine.** One node: 8 vCPU, 32 GiB of memory and 100 GiB of disk, plus 120 GiB of volumes. What it costs, plainly: every
>    Sindri update restarts the whole pod, and Onyx takes several minutes to answer again; one machine is one point of failure; there is
>    only one copy of Sindri.
> 5. **Chat and tickets.** You connect your chat and ticket systems to Onyx once; Sindri reads Onyx on its own schedule and links what
>    it finds to the code and features in the map. Anything in the pod could register on Onyx's sign-up page before you do, so make your
>    own sign-up soon after install (the install notes say when).
> 6. **Always fresh, no model.** Sindri pulls your repos' main branches every minute and keeps its maps current. It needs no language
>    model (K12).
> 7. **Safety rule.** Nothing but Sindri's own container can open Sindri's data volume, because it holds the file that proves
>    it is you approving things.

Defaults the earlier revisions and this one took where the owner's answers left a choice (each is reversible without a redesign):

| Default | Where | The other way |
|---|---|---|
| Probes are `exec` probes running `curl --max-time 4` against `127.0.0.1:<port>/sindri/ping` inside Sindri's container, with the timings of C.1 | C.1 | later, a tiny `sindri ping` verb in the image (no curl process per probe); an `httpGet` probe cannot reach a loopback-only listener |
| nginx listens on a high loopback port; Sindri talks to Onyx through nginx | A.3 | Sindri calling api_server directly (needs the Onyx adapter to drop the `/api` prefix); K-S0 verifies the default |
| Onyx's pinned images are per-platform digests, chosen by `onyx.platform` (default `amd64`) | A.3 | an index digest row in `onyx.lock`, if K-O1 adds one |
| OpenSearch runs with `bootstrap.memory_lock=false` and bound to loopback | A.3 | none if K-S0 shows the bootstrap checks only warn |
| nginx finds Onyx's upstreams through `ONYX_BACKEND_API_HOST` and `ONYX_WEB_SERVER_HOST` set to `127.0.0.1`, and its port is moved by a `sed` in the container command | A.3 | `hostAliases` on the pod, if K-S0 shows the variables don't take effect |
| A profile key (`repo-pull.allowedHosts`, `index.graph.enabled`) changes only through `sindri-pod.sh profile edit`; no chart value sets one | C.1, C.3 | none needed |
| Backup, a disruption budget and a network policy are left out of the chart in v1 | C.4, C.5, K7 | each comes back without a redesign: snapshots as a CronJob, `maxUnavailable: 1`, a policy limiting the pod's egress |

| # | Question, in plain words | Answer | Why, and the cost |
|---|---|---|---|
| **K1** | How does Onyx come up beside Sindri? | **Decided (q73): as sibling containers of the same pod, from Onyx's prepackaged images by digest (`context/onyx.lock`), in one chart** (§A.3). Not Onyx's Helm chart, not Docker Compose in the pod. **Nothing is privileged; no Docker-in-Docker, no Docker CLI in the image** | The owner's answer. The cost: the chart carries a container per Onyx service, so it must stay in step with Onyx's Compose file; that is why the containers are **rendered by a script** from the pinned Compose file and the lock, with a CI drift check (K-O1). Pods also lose Docker's private network: the containers use the pod's one loopback (§6) |
| **K2** | Onyx's database: inside the cluster, or a managed database? | **Decided by K1: inside the pod**, Onyx's own Postgres container (`relational_db`) on a volume claim | No operator, no cluster-wide resources, nothing for a cluster admin to install. The manual backup procedure (K7) covers this claim too |
| **K3** | Who can reach Sindri's API? | **Decided (q80, q83): only the pod's own loopback.** No Service, nothing listens on the pod's address, so no NetworkPolicy is needed for ingress | Nobody outside the pod can reach Sindri at all. `kubectl port-forward` and `exec` (the owner's) arrive from inside the pod's network namespace. The listeners stay exactly as on a laptop (`ServePort.cs:35` and `LoopbackBinding.cs:40`, loopback only), so the network needs no code change. Where agents run, and how a caller outside the pod would get in, is out of scope (B.4) |
| **K6** | How does the pod get the code, and how fresh? | **A new Sindri service, `repo-pull`,** fast-forwards each repo's default branch every minute with a read-only token from a Kubernetes secret; `repo add` records the repo, and the first clone runs after the approval, because the service reads only the approved profile; a repo whose directory is missing is cloned again | Sindri never clones today and expects a checkout on disk. The common git-sync sidecar lays a repo out as linked worktrees, which Sindri resolves to a main checkout without a tree; a service of our own keeps the layout Sindri already checks (clean, HEAD in main). |
| **K7** | Where does the state live, and what is the backup? | **Three volume claims**: Sindri's (`/data`), Onyx's (Postgres and the search index; one claim, subPaths) and Ollama's (its model weights). **One copy of the pod** (a StatefulSet of one). **The backup is a manual procedure in deploy.md for v1**: no CronJob and no RBAC in the chart (a reversible default, §11) | Sindri's ledger, views, map and Temporal's history are SQLite files that allow one writer; two replicas would fight over the lease. The procedure snapshots Sindri's and Onyx's claims at the same moment (a `VolumeGroupSnapshot` where the cluster has it, else two `VolumeSnapshot`s seconds apart), so the map's Onyx cursor and Onyx's database stay at the same point; Postgres recovers from a crash-consistent snapshot as from a power cut; two snapshots seconds apart can leave the cursor a little ahead of Onyx's database, and the sync service resyncs, as it does after any restore. The snapshots hold the human token, the ledger, Onyx's database and the connector OAuth credentials (§6), so the namespace is the owner's alone and the StorageClass should be encrypted where the cluster has one. **The procedure also exports Onyx's Secret** (`kubectl get secret … -o yaml`, stored as carefully as the snapshots: it holds the key to the stored connector credentials), because a restore into a new namespace, or a reinstall that lost the Secret, is unusable without it (§A.3 Secrets). |
| **K8** | How does the owner see the app on the pod? | **`scripts/sindri-pod.sh connect`**: one command that port-forwards, fetches a fresh human token over `kubectl exec`, and opens the desktop app against the pod | Today the app only reaches a Sindri whose files it can read on the same machine. A browser version of the app is still later (UI spec §12.2) |
| **K9** | Where do secrets go? | **Kubernetes secrets as environment variables** (`env:` pointers): one read-only git token per git host; never in the image, the profile or the chart's values. Onyx's own secrets (its database password, its OpenSearch admin password, its auth secrets) are a second Secret the chart creates on first install (random, kept across upgrades), mounted into Onyx's containers only. In pod mode Sindri's child processes (git, tools) get only an allowlisted environment, not every secret (§A.2 A2d) | Secrets mounted as files are symlinks in Kubernetes, and Sindri's `file:` pointers refuse symlinks on purpose. The Onyx token Sindri makes at attach is written to Sindri's volume (`secrets/`, owner-only), as the managed mode does today when no keychain exists. Onyx's Compose file falls back to well-known passwords (`password`, `StrongPassword123!`) and keys (`minioadmin`) when nothing is set; the render script refuses to emit a container that would use `minioadmin` or any other well-known fallback (A.3, Rendering). |
| **K10** | Embeddings for the code index (local models only)? | **An Ollama sidecar** in the Sindri pod on `127.0.0.1:11434`, its model pulled once onto Ollama's own claim **by digest** and checked against `context/code-index.lock` before Sindri serves queries (doctor shows the result) | Sindri allows only a loopback Ollama; a sidecar uses the pod's loopback, so no code change |
| **K11** | Images: built how, pinned how? | **Two locks, one per source.** Our own image is built by CI from the repo for linux/amd64 and linux/arm64, pushed to a registry from config, and referenced **by digest** in the chart. Our own and the base images, and Ollama, are pinned in `deploy/images.lock` (in `onyx.lock`'s style, with a `check` script). Onyx's are pinned in `context/onyx.lock`, pulled by the kubelet by the digests it holds and rendered into the chart (K-O1). A `helm template` test asserts every image reference in the rendered chart is a digest that appears in one of the two locks | One lock per source of truth. `onyx.lock` records one digest per platform; a per-platform manifest digest pulls only on that platform, so the chart picks the row by `onyx.platform`. Model weights are pinned too (K10, §A.3 Egress). |
| **K12** | Does the pod get a language model (for labelling the map and the decisions pass)? | **Decided: no model in Sindri v1.** The map, the code index, the docs index and every query work; only labelling suggested links and the decisions pass (with its adjudication) need a model, and they stay off; doctor says so. **Sindri's image holds no model, no agent CLI and no login** | The owner's answer. Sindri's model port reaches only the owner's own agent CLIs on the same machine (`JudgeModelPort`), and its image ships none. The pod's map therefore carries records and links but no model-made labels. A model provider for the pod is its own later spec (K-I1) |

## 2. Part A: the image and the pod

### A.1 The Dockerfile

At the repo root, multi-stage, every `FROM` by digest (from `deploy/images.lock`):

1. **build** (`mcr.microsoft.com/dotnet/sdk:10.0.401-noble@sha256:…`, the SDK `global.json` pins): restore with the locked
   restore, `dotnet publish src/Sindri/Aw.Sindri.Host -c Release --no-self-contained -p:AwRecipesDir=<recipes/sindri>`, the same
   flags `scripts/sindri-app.sh` uses; `judge` and `aw-packets` NativeAOT for the target platform (`AW_SINDRI_JUDGE`,
   `AW_PACKETS_BIN` point at them); the shipped recipes' evals run (`recipe eval --shipped`) and fail the build as they fail
   `sindri-app`. `sindri-ui` is not built: the image has no desktop.
2. **tools**: `temporal` from `binaries.lock` (download, sha256 check, as `scripts/binaries-lock.sh fetch temporal` does), and
   graphify at the code index's pin in a venv (`uv`, as `scripts/install-deps.sh` does).
3. **runtime** (`mcr.microsoft.com/dotnet/aspnet:10.0-noble@sha256:…`): `git`, `bubblewrap`, `tini` as PID 1 (it reaps the
   detached Temporal process), `procps` (Sindri's `DevServerProcesses` runs `ps` and `kill` for the Temporal dev server on Linux
   too), `curl` (the exec probes of C.1), the three stages' outputs under `/opt/sindri`, a fixed non-root user, `ENTRYPOINT ["tini","--","/opt/sindri/start.sh"]`,
   where `start.sh` copies the temporal binary to where Sindri looks for it
   (`$AW_STATE_DIR/cache/bin/temporal-<ver>/temporal`, with no override) whenever that versioned directory is missing (a new
   volume, or a new temporal pin after an upgrade) and then `exec`s `/opt/sindri/sindri serve`. Every variable a `kubectl exec`
   needs is image `ENV`, not set by `start.sh` (an exec doesn't run it): `AW_STATE_DIR=/data/state`,
   `AW_SINDRI_HOME=/data/state/sindri-app`, `HOME=/data/home`, `AW_SINDRI_DEPLOY=pod`; the chart adds the rest. Read-only root
   filesystem; `/data` and `/tmp` writable; the git and `uv` and graphify caches live under `/data` (a read-only root has no other
   writable home). `/opt/sindri` is world-readable. The image has no Docker CLI, no Compose plugin and no daemon.

`serve` runs in the foreground, ignores SIGHUP and stops cleanly on SIGTERM (it already does: `ServeCommand.cs`), stopping its
Temporal dev server within 10 s. `serve`'s Onyx hooks (`OnyxServeHooks`) act only on a Sindri-**managed** Onyx and "an unmanaged
Onyx is never touched", so in the pod, where Onyx is unmanaged (§B.3), a Sindri restart leaves Onyx's containers running: no code
change. When the whole pod stops, Onyx's containers stop with it, at the same time (all are ordinary containers, so every container gets the stop signal at once). The pod's
grace period is 120 s: Sindri's own stop is at most about 30 s (20 s for launched sessions, 10 s for Temporal); Postgres and
OpenSearch get the rest to flush; K-S0 times a pod delete with Postgres under write load.

### A.2 What the image needs from Sindri (code changes)

| # | Change | Where | Why |
|---|---|---|---|
| A2a | A **pod mode**, `AW_SINDRI_DEPLOY=pod` (unset: today's desktop behaviour, byte for byte; in pod mode telemetry export is on, to the endpoint in `OTEL_EXPORTER_OTLP_ENDPOINT`, which the chart sets from `telemetry.endpoint`) | `SettingsFromEnvironment`, `Profile` (`deploy:` read-only echo for doctor) | one switch that the changes below key on; a desktop install can't enter it by accident because nothing sets it |
| A2b | **No new host variable for the lease.** The lease records `Environment.MachineName` (`PosixProcessProbe`), which in a StatefulSet pod is already its stable name (`sindri-0`); a restarted pod then has the same host; on the same node `LeaseDecision.Liveness` proves the old holder dead by its process start time (container pids repeat and the boot id is the node's), and on another node by the boot id. A force-deleted pod or a node that comes back with the same boot id can read as provably dead while the old process still holds the volume, so deploy.md says never to force-delete `sindri-0`, and the heavy restart test (§7) covers a normal rollout and a force delete. The test is the proof; only if it shows a 60 s wait does a builder add a pod-mode host override | `Infrastructure/Lease` (test only) | removes a setting the code didn't need |
| A2c | Doctor rows for pod mode: the volume writable, owner-only and under 70 % full, the temporal binary present, `bwrap` usable (user namespaces), Ollama answering, **Onyx's containers ready** (K-O2), **Onyx reachable** (its health route at the attached URL) and **the pin matches Onyx's `/api/version`** (the existing `OnyxSettings.CheckVersion`), the served repos (the mapped set, which `context.repos` can narrow below the approved profile), the model-less steps listed as off (K12), the map's lag behind each repo's main branch. **When Onyx is off** (`AW_SINDRI_ONYX_ENABLED=false`, which the chart sets with `onyx.enabled: false`) the Onyx rows read "Onyx: off in this install (code and docs only)" at ok level, with no "not set up" and no command to run. Sindri's pod has no Kubernetes service account (`automountServiceAccountToken: false`), so "containers ready" can't ask the API server: doctor opens each Onyx container's loopback port from a list the chart passes (`AW_SINDRI_ONYX_PORTS`, `name:port` pairs written by K-O1's render, the same one that wrote the containers) and the row names the ones that don't answer. On a fresh install a row whose thing isn't set up yet (Onyx before `onyx attach`, no repos, no map yet) reads "not set up" with the next step, never fail; rows are ordered as the owner sets things up (volume, repos, map, Onyx), and each warn or fail names its fix | `Features/Doctor`, `Infrastructure/Onyx` | the owner's first check after install, through `kubectl exec … sindri doctor` |
| A2d | **Pod-mode child environment**: one environment factory that every `ProcessStartInfo` in `src/Sindri` goes through (today about 20 files spawn processes and inherit the whole environment, among them `ProcessToolRunner`, `Workflow/GitProcess`, `DevServerProcesses` for the detached Temporal server, `GitChangeSource` and `ProcessClaudeCli`); in pod mode it passes only an allowlist (`PATH`, `HOME`, `LANG`, the one credential a call needs), and an architecture test fails on any `new ProcessStartInfo` outside it | `Infrastructure/Processes` plus each spawner | the env-pointer secrets of K9 must not reach every child |
| A2e | **Onyx in pod mode is attached, never managed.** (1) `sindri onyx up` and `down` in pod mode refuse with `SND-ONYX-013` (the highest code on main is `SND-ONYX-012`; K-O2 re-checks at merge) whose fix text is "Onyx is part of this pod. Run `sindri-pod.sh connect --onyx`." (2) `onyx attach [--url U]` (§B.3). (3) No preflight change: the preflight is managed mode's (Docker, Compose, memory and disk on the machine) and never runs for an unmanaged Onyx, which is why revision 2's memory, disk, stop-hook and fix-text changes are gone. The pod's memory and disk are the chart's requests, limits and claim sizes, measured in K-S0 | `Features/OnyxAttach`, `Host/Commands/OnyxVerbs` | without (1) a stray `onyx up` in the pod would look for Docker and fail with a Docker message |
| A2f | **Temporal runs headless in pod mode**: `TemporalDevServer` starts the dev server with `--headless` and no `--ui-port` (Sindri never uses the UI); the frontend and HTTP ports stay on `127.0.0.1`. Temporal's ports are not fixed: `TemporalDevServer` picks free loopback ports at random at each start (`PickPorts`), and `DevServerProcesses.Parse` reads them back from `ps`. **K-S0 confirms** the flag's real spelling in the pinned `temporal` (`temporal server start-dev --help`) and that `--headless` with `--ui-port` omitted still takes `--http-port` | `Workflows/TemporalDevServer` | the unauthenticated Temporal UI is one less thing on the pod's loopback (§6) |

If the cluster forbids user namespaces, `bwrap` can't start and the code graph can't build (`SND-CODEIDX-007`); doctor says so, and the owner sets the profile key
`index.graph.enabled: false` with `profile edit` (§C.3; it is a profile key only, and no chart value sets it). The text and symbol index still work. Under a container runtime's default seccomp
profile `bwrap` usually cannot create a user namespace, so Sindri's container (which keeps the default profile; **no container in
the pod is privileged**) will run with the graph off; the kind run records which it is, and `docs/sindri/deploy.md` names the one
seccomp option an admin may choose.

### A.3 Onyx's containers in the pod

Onyx ships a Compose file, and its images are prepackaged. The pod runs **those images** as sibling containers, one per Compose
service, each by the digest `context/onyx.lock` pins (the lock has one `image` row per repository and platform). Nothing here runs
Docker, mounts a socket or asks for a privilege.

| Container (Compose service) | Image (`onyx.lock` row) | Listens on (pod loopback) | Role |
|---|---|---|---|
| `api_server` | `onyx-backend` | `127.0.0.1:8080` | Onyx's API; runs the database migration at start |
| `background` | `onyx-backend` | none | connectors and indexing workers |
| `web_server` | `onyx-web-server` | `127.0.0.1:3000` | Onyx's page |
| `inference_model_server` | `onyx-model-server` | `127.0.0.1:9000` | embeddings, for queries and for indexing (the one model server, as in `onyx up`) |
| `relational_db` | `postgres:15.2-alpine` | `127.0.0.1:5432` | Onyx's database |
| `opensearch` | `opensearch:3.6.0` | `127.0.0.1:9200` (HTTPS with a demo certificate once the admin password is set), `9300` (transport), `9600` (performance analyzer) | the search index |
| `cache` | `redis:7.4-alpine` | `127.0.0.1:6379` | Onyx's cache (no persistence, as in the Compose file) |
| `nginx` | `nginx:1.25.5-alpine` | `127.0.0.1:<onyx.port>` (see below) | the one front door Sindri and the owner's browser use |

The pod's whole port map, written as data by `onyx-pod.mjs` and diffed by the kind test against the pod's listeners (read once, below):
the rows above, plus Sindri's API port `7410` (loopback only; **nothing in the pod listens on the pod's address**), Temporal's frontend and HTTP ports and its internal service ports (all loopback; **picked at random by `TemporalDevServer` at each start**, so the map says "Temporal: recorded in `control.json` and `sindri status`", not a number; headless drops only the UI port), and Ollama `11434` (loopback). The kind test reads the pod's listeners **once**, because all the containers use one network namespace, so one read sees every listener: `deploy/test/listeners.sh`, run with `kubectl exec` in the Sindri container, reads `/proc/net/tcp` and `/proc/net/tcp6` (state `0A`, listening) and matches each socket's inode to a process through `/proc/<pid>/fd` in that container's own process table. It excludes the listeners owned by the `temporal` process (their ports are random, and its internal service ports are not in `sindri status`), asserting each is on `127.0.0.1`, asserts that no listener of any container is on the pod's address, and compares everything else (the sockets it cannot map to a process, which belong to the other containers) with the map exactly. It reads `/proc` instead of running `ss` from a `kubectl debug` ephemeral container because the runtime image has no `iproute2` (adding it only for a test grows the image), and an ephemeral container needs an image that has `ss`, its pull, and the API right to create one; `/proc` needs none of those. The main containers start together (Start order below).

`minio` (the `s3-filestore` profile), `code-interpreter` and `indexing_model_server` are **not** run. Sindri's use of Onyx needs
none of them: the one model server embeds for indexing too, with `INDEXING_MODEL_SERVER_HOST` pointing at it, as `onyx up` does
(which also removes the stock file's clash of both model servers on port 9000), and the code-interpreter's Compose service is the
one that asks for `user: root` and carries a commented-out `privileged: true`. Without minio the render also sets what pointed at
it: `FILE_STORE_BACKEND` goes to Onyx's database-backed file store (`postgres`; **K-S0 confirms** v4.9.0 accepts that value, and if
no store without S3 exists K-S0 stops and tells the owner, §7), and every `S3_*` and `CODE_INTERPRETER_*` line is **dropped** (listed
as dropped, so `--check` is satisfied and no `minioadmin` is ever emitted). The chart has no `onyx.s3Filestore` or
`onyx.codeInterpreter` switch in v1.

| Aspect | Rule |
|---|---|
| Images | Each container's `image` is `repository@sha256:<digest>` from `onyx.lock`, picked by `onyx.platform` (`amd64` by default; a pod on arm64 nodes sets `arm64`), because the lock records a manifest digest per platform and a per-platform manifest pulls only there. The render script fails if a service of the pinned Compose file has no `image` row for the platform, and `scripts/onyx-lock.sh verify` (maintainer-run) keeps meaning what it means today |
| Rendering (K-O1) | `scripts/onyx-pod.mjs` reads these inputs, all committed: `context/onyx.lock`; `deploy/onyx/docker-compose.yml`, `deploy/onyx/env.template` and `deploy/onyx/data/nginx/*` (copies of the pinned upstream files, each sha256 checked against the lock's `file …` row, as the Compose copy is); the sizing file (next row); and `deploy/onyx/pod-overrides.yaml` (only what is pod-only: loopback hosts and binds, ports, the nginx `sed`, the file store, secrets, volumes, probes, and the Compose-only constructs, which stay out of the sizing file). It writes `deploy/helm/sindri/templates/onyx-containers.yaml`, the nginx ConfigMap and the port map (`AW_SINDRI_ONYX_PORTS`). `--check` first verifies every input against the lock, then re-renders to memory and fails on any difference, the ConfigMap included (CI, and a bash test in AGENTS.md's list). It also fails when an `environment:` line of the Compose file or a key of `env.template` is not carried over, overridden or listed as dropped, so a new Onyx setting can't slip in unnoticed. A pod has no `.env`, so `env_file: .env` becomes explicit `env` entries from `env.template`: `AUTH_TYPE=basic`, `DISABLE_TELEMETRY=true`, `WEB_DOMAIN=http://127.0.0.1:<onyx.port>` (the address of the port-forwarded sign-up; `connect --onyx` forwards the same local port by default, §C.3) and the rest as the template has them. The render **refuses** `minioadmin`, `password`, `StrongPassword123!` and every other well-known fallback: a credential variable (name has PASSWORD, SECRET, KEY or TOKEN) whose Compose default isn't covered by the Secret or the dropped list. `--check` also has a **required-keys list** for the secrets that have no Compose default and would otherwise be empty or stored in the clear: `USER_AUTH_SECRET` (the API server refuses to start without it) and `ENCRYPTION_KEY_SECRET` (it encrypts connector credentials; unset, they sit unencrypted in Postgres, which snapshots copy), besides `POSTGRES_PASSWORD` and `OPENSEARCH_ADMIN_PASSWORD`; a render that doesn't take each of them from Onyx's Secret fails. **An Onyx upgrade is:** bump `context/onyx.lock` (`onyx-lock.sh verify`), refresh the committed copies, run the script, read the diff, `helm upgrade` |
| One recipe for managed and pod Onyx | `sindri onyx up` already ships tested overrides for one user (`OnyxConfig.cs`, `docs/sindri/onyx.md` §2): one model server (no indexing model server, `INDEXING_MODEL_SERVER_HOST=inference_model_server`), the Slack and Discord bots kept from starting, the celery worker concurrencies at 1, OpenSearch's heap at 1g with a 2g limit. The pod starts from the same values. **Only the values that carry over to a pod are kept in common**, in one data file, `context/onyx-sizing.json`: the environment values per service, the memory and heap numbers, the concurrencies, which services run, and the bot flags **as environment** (K-S0 confirms v4.9.0 has such flags; if it has only the stock `background` command that seds `supervisord.conf` into `/tmp`, that command stays in `pod-overrides.yaml` and in `OnyxConfig`, the file records only that the bots are off, and the drift test compares that flag; the `background` container then needs a writable `/tmp`). **Compose-only constructs are not in the file**: `ports: !override`, `profiles`, `depends_on … required: false` and shell commands stay in `OnyxConfig` (C#) and in `pod-overrides.yaml`. `OnyxConfig` (in `Aw.Sindri.Core`) takes the file's content as a parameter, embedded from Infrastructure the way the lock is; `onyx-pod.mjs` reads it from disk. **Sizing-class keys are an explicit list**, held in the file itself (`sizingKeys`, one line of reason each): the three celery worker concurrency variables `OnyxConfig` sets (`CELERY_*_CONCURRENCY`, the exact names copied from `OnyxConfig`), `OPENSEARCH_JAVA_OPTS` (the heap), OpenSearch's memory limit, the set of services that run, and the bot-off flags. The file holds those keys and no others. **Host and wiring keys are not sizing and may differ** between the two sides: `INDEXING_MODEL_SERVER_HOST` is `inference_model_server` in Compose and `127.0.0.1` in the pod, and so are the other `*_HOST` variables, `WEB_DOMAIN`, `INTERNAL_URL`, ports, binds and the Secret-fed variables; neither test looks at them. **The drift check compares only the list:** a C# test renders `OnyxConfig`'s override and asserts each listed key is present with the file's value and that the file holds no key outside the list; `onyx-pod.mjs --check` asserts the same for the pod's containers, except that the memory key is a floor (the pod's limit is at least the file's value, because the pod sets its own limits from K-S0's measurement, Resources row). So a listed value changed on one side fails one of the two tests, and a key outside the list can't be in the file. The lock's `docker-memory-bytes` (5.5 GiB) was measured for the stock set; K-S0 re-measures it for this one |
| Reaching each other | Compose reaches services by name on a private network; in a pod they use one network namespace, so the script sets each host variable the Compose file reads to `127.0.0.1`: `POSTGRES_HOST`, `OPENSEARCH_HOST`, `REDIS_HOST`, `MODEL_SERVER_HOST`, `INDEXING_MODEL_SERVER_HOST`, `API_SERVER_HOST` (all appear in the pinned Compose file as `${NAME:-service}` defaults), and the web server's `INTERNAL_URL` to `http://127.0.0.1:8080` (its Compose default is `http://api_server:8080`). nginx's upstream names come from two variables its start script reads: `run-nginx.sh` exports `ONYX_BACKEND_API_HOST` (default `api_server`) and `ONYX_WEB_SERVER_HOST` (default `web_server`) and `envsubst`s them into the template's `server ${ONYX_BACKEND_API_HOST}:8080` and `server ${ONYX_WEB_SERVER_HOST}:3000` (read in the pinned copies), so the script sets both to `127.0.0.1`: no template edit, so the pinned hashes stay valid. `hostAliases` on the pod (those names to `127.0.0.1`) is the fallback only, if K-S0 shows the variables don't take effect |
| Ports | One model server, so the stock file's clash (both model servers on 9000) is gone. Every port in the map above is distinct, and the kind test diffs the map against the pod's listeners, read once (above) |
| nginx | The Compose file publishes nginx's port 80 as `${HOST_PORT:-3000}:80` and `${HOST_PORT_80:-80}:80`, and its health check calls `127.0.0.1:80`. In a pod there is no publishing, nginx listens in the pod, and port 80 needs root. The default is a **high port on loopback** (`onyx.port`, default 8081), moved **in the container's command, not in the committed templates**: the template hard-codes `listen 80 default_server;`, so the command (`cp -rL /nginx-templates/. /etc/nginx/conf.d/`, then `run-nginx.sh`) gets one `sed` between the two; `-L` replaces Compose's `cp -a` because the templates are a ConfigMap mount whose entries are symlinks (`..data`), and `-L` copies plain files for the `sed` to edit that rewrites that line in the copied `app.conf.template` to `listen 127.0.0.1:<onyx.port> default_server;`. The ConfigMap keeps the pinned files byte for byte, so the sha256 rows in `onyx.lock` stay valid; the script writes the liveness probe against the new port (`/nginx-health`). nginx needs a writable `/etc/nginx/conf.d` (the `cp` target), `/tmp`, `/var/cache/nginx` and `/run`: they are `emptyDir`s, each with a `sizeLimit` of 128 Mi (C.1 Storage), so the root filesystem need not be writable. K-S0 verifies the `sed` and the start script. The alternative is Sindri calling `api_server` directly on `127.0.0.1:8080`, which drops nginx from Sindri's path but not from the owner's browser and would need the Onyx adapter to stop adding the `/api` prefix nginx strips (a code change in `Aw.Context.Infrastructure`). The pod sysctl `net.ipv4.ip_unprivileged_port_start=0` is pod-wide and refused or unsafe on some clusters, so it is the last resort. Sindri reaches `http://127.0.0.1:<onyx.port>`, and so does the owner through `kubectl port-forward` |
| Nothing on the pod's address | In Compose these services sit on a private network. In a pod they would listen on the pod's address, reachable by any pod in the cluster. So **every Onyx listener is set to loopback**, and K-S0 proves it with the one listener read (above) and a scan of the pod's address from another namespace, which must find nothing (§6): Postgres `-c listen_addresses=127.0.0.1` (appended to the Compose command `-c max_connections=250`); Redis `--bind 127.0.0.1` (appended to the Compose command); OpenSearch `network.host=127.0.0.1` (and `http.host`, `transport.host`); the API server's `uvicorn … --host 127.0.0.1` (the Compose command has `--host 0.0.0.0`; the script rewrites it); the web server `HOSTNAME=127.0.0.1` (a Next.js setting; **unverified for this image**); the model servers' `--host`; nginx's listen address. A setting an image does not honour is a K-S0 finding, answered with a different image setting or, if there is none, by the stop rule of §7, and the spec is amended before K-O1 |
| OpenSearch | The Compose file sets `bootstrap.memory_lock=true` and `memlock` and `nofile` ulimits. A pod's container cannot set ulimits (the runtime's defaults apply), so the script sets `bootstrap.memory_lock=false`, keeps `discovery.type=single-node`, the heap from the sizing file (`-Xms1g -Xmx1g`) and the admin password from the Secret. **To be verified in K-S0:** with the node bound to loopback only, OpenSearch is in development mode, so the `vm.max_map_count` and file-descriptor bootstrap checks only **warn**; if the pinned image still refuses to start, the fallback is an init container that raises `vm.max_map_count` (which is privileged, so it would break this spec's rule) or a node setting the deployer makes (a node-wide `vm.max_map_count` of 262144, which deploy.md lists). K-S0 records which, and the warning's effect on the index's speed |
| Security contexts | **No container is privileged**, `allowPrivilegeEscalation: false` everywhere, `automountServiceAccountToken: false` on the pod. **Every container drops ALL capabilities and adds back only what K-S0 proves its entrypoint needs** (recorded per container; `NET_RAW` never). The namespace carries Pod Security level **`baseline`** (or none): `baseline` forbids privileged containers, host namespaces and host paths, and allows images that start as root. The spike and the kind run label their namespace `pod-security.kubernetes.io/enforce=baseline`, so "baseline is enough" is proved, not asserted. **Which containers can run non-root is unverified and K-S0 records it per container**; the expectation, to be confirmed: `postgres`, `redis`, `nginx` and `opensearch` entrypoints start as root and drop to their own user (Postgres needs `CHOWN`, `SETUID` and `SETGID` for its data directory unless the volume is already owned by its uid and the pod sets `fsGroup`); the Onyx images may or may not already run as a non-root user. `restricted` is the goal wherever an image allows it (non-root, `RuntimeDefault` seccomp), the chart sets those fields per container from values, and the namespace label is whichever level the weakest container needs. deploy.md says which containers are the reason it is `baseline` |
| Start order | **No Onyx container is a native sidecar, and none gates Sindri.** All containers, Sindri's and Onyx's, are ordinary containers that start together; the only init container is the one that prepares the volume directories (Volumes row). **Sindri's container is first in the pod spec, and that init container runs Sindri's own image**: the kubelet pulls and starts in spec order, so Sindri starts as soon as its image is there, while the Onyx images are still pulling (an init container on another image would add a pull in front of everything). K-S0 and the kind run measure **time-to-Sindri-up on a cold node** (`/sindri/ping` answering before the last Onyx image finishes pulling) and record it. Compose's `depends_on` is not turned into pod ordering: Onyx's own images retry (`api_server` runs `alembic upgrade head` first and restarts under Kubernetes' restart policy if the database is late; nginx's start script waits for the API server; the model servers are `service_started`, not `service_healthy`, in Compose), and **K-S0 verifies** that the pod settles with no ordering at all. Why: Sindri must be up in seconds even while ~11 GB of images are still pulling or OpenSearch is crash-looping, so the owner can run `doctor` and approve a profile. **Probes:** Onyx containers get **liveness probes only** (generous, from their Compose health checks where Compose has one, with long start periods; Compose has **no health check for OpenSearch**, so its liveness probe is one we write: a TCP check on `9200`, because the port answers HTTPS with a demo certificate) and **no readiness probes**; Sindri's readiness is an exec probe of `/sindri/ping` on the loopback (C.1). There is no Service and nothing routes by the pod's Ready condition, so a crash-looping Onyx container (which makes the pod not Ready) does not stop Sindri from answering, and the answer carries Onyx's degraded note (`SND-CONTEXT-010`). Sindri's readiness probe decides only the pod's Ready condition (what `helm --wait` and `kubectl rollout status` read). **The StatefulSet sets `podManagementPolicy: Parallel`.** With the default `OrderedReady` the controller does not roll a corrected template over a pod that is not Ready (its reconcile stops before the update step; Kubernetes' StatefulSet documentation calls this the forced rollback), and a looping Onyx container is exactly such a pod, so the `helm upgrade` that fixes it (raise a limit, fix an env var) would never apply. With one replica `Parallel` costs nothing (it changes only how several pods are created and deleted) and the update step replaces an outdated pod without waiting for it to be Ready. The chart test asserts the field, and the kind run breaks an Onyx env var, runs `helm upgrade` with the fix and expects the pod to roll with no manual delete. If a cluster's controller still waits, the fallback is `helm upgrade` and then `kubectl delete pod <pod>` (a normal delete, never forced, A2b). Install and upgrade both run `--timeout 30m` and without `--wait`. The pod-state table (C.1) says what each state means |
| Volumes | Onyx's named volumes become **one claim** (`onyx-data`, default 60 Gi) with a `subPath` for each that must persist: `db_volume` (Postgres; PGDATA is a subdirectory so the filesystem's `lost+found` doesn't break `initdb`) and `opensearch-data`. **The model caches get no volume over the image's baked path.** Docker seeds a new named volume from the image; a Kubernetes volume at that path would hide whatever the image baked in and force a download at start. K-S0 lists where each model server's weights live and whether they download at start: if baked in, the path stays as the image has it (a writable `emptyDir` only if the process writes there); if they download, a volume is allowed and the weights are pinned (Egress row). Postgres gets a memory-backed `emptyDir` at `/dev/shm` (1 Gi, from Compose's `shm_size: 1g`, counted in its memory limit). The log volumes become `emptyDir`s with a `sizeLimit` of 512 Mi (logs also go to stdout); `minio_data` and `file-system` aren't used; Redis keeps its Compose `tmpfs`. **subPath directories** the kubelet creates are often root-owned and outside `fsGroup`, so one plain, unprivileged init container (Sindri's image, which has a shell, `mkdir` and `chown`) creates each directory and sets its owner to the uid that container runs as (adding `CHOWN` and `FOWNER` only if K-S0 shows the claim's filesystem needs them; K-S0 verifies each directory). The pod sets `fsGroup` and `fsGroupChangePolicy: OnRootMismatch`. nginx's `../data/nginx` bind mount is a ConfigMap rendered from the committed copies of the files whose sha256 `onyx.lock` pins. The claim is part of the backup procedure (K7) |
| Secrets | A Secret the chart creates on first install (random values, kept across `helm upgrade` by a `lookup`) holds `POSTGRES_PASSWORD`, `OPENSEARCH_ADMIN_PASSWORD` (which the Compose file passes to OpenSearch as `OPENSEARCH_INITIAL_ADMIN_PASSWORD`), `USER_AUTH_SECRET` and `ENCRYPTION_KEY_SECRET`, each by name (the last two have no Compose default, so `--check`'s required-keys list covers them, Rendering row), and is mounted into Onyx's containers only, never into Sindri's or Ollama's. Because all the containers use the pod's one loopback these are not a boundary against another container of the pod (§6); they stop the well-known Compose defaults from being the real passwords. **The Secret carries `helm.sh/resource-policy: keep`**, so `helm uninstall` leaves it beside the claims, and a reinstall with the same release name and namespace finds it by `lookup` (Helm adopts it; the values stay in step with the data on the kept claims). **`onyx.existingSecret`** names a Secret the owner made with the same keys: the chart then makes none and skips `lookup`; it is **required when the chart is rendered with `helm template` and applied** (Argo CD, Flux and the like), whose renderer sees `lookup` empty and would re-randomize the values on every sync. **A reinstall without the Secret** (deleted by hand, or the namespace was) generates new values: Postgres rejects the new password (the old one is in its data directory) and every stored connector credential is undecryptable (the encryption key changed), so Onyx does not come up usable; the fix is to restore the exported Secret (K7), or, if it is lost, to start Onyx over from an empty claim (deploy.md, which warns it deletes everything Onyx holds). `lookup` returns nothing under `helm template` and `--dry-run`, so the CI render test sees fresh random values on every run: it compares the Secret's **keys**, not values, and the heavy kind test proves the values survive a `helm upgrade` and an uninstall followed by a reinstall on the kept claims (Onyx still starts) |
| Resources | Every container sets requests **equal to** limits, so the pod is **Guaranteed QoS** and node pressure doesn't pick it first (an eviction would take Sindri down with Onyx). **The pod's footprint is Onyx plus Sindri plus Ollama and must fit one node: about 7 vCPU and 25 GiB of requests (C.1).** The numbers for Sindri and Ollama come from K-A1's `docker run` (K-C1 ships with them), Onyx's from K-S0's measurement (K-O1 adds them), and they become the minimums in `values.schema.json`, not prose. The sum of the memory limits is at least K-S0's re-measured figure for the sizing set (the lock's `docker-memory-bytes`, 5.5 GiB, is for the stock set), with OpenSearch's limit at least twice its heap.  The starting requests and limits (C.1), `ephemeral-storage` ones included (C.1 Storage has Onyx's per-container numbers, 12 Gi together), are sized for it and are the minimums in `values.schema.json`; the kind run and K-S0 record the measured CPU, memory and disk, which may lower the numbers in a later chart revision but do not gate the node choice |
| Egress | The kubelet pulls Onyx's images (about 11 GB across the pod; the registries `onyx.lock` names), so the pod's first start takes minutes. At run time the pod needs the git hosts, Onyx's connectors' services (the chat and ticket systems the owner connects), and, if the model servers fetch their weights at start (K-S0 records it, Volumes row), the model hub's host. **Weights are pinned:** the Hugging Face revision of each model is set by an environment setting K-S0 names, and Ollama's model is pulled by digest; each is verified before Sindri serves queries (doctor shows the result). The kind run records every host the pod contacts at start and deploy.md allowlists exactly those. Onyx's web connector can fetch cluster-internal addresses too, **and the pod's own loopback** (§6), so the connector checklist says to give it only public URLs |
| Not run in the pod | `onyx up`, `onyx down`, `docker`, `docker compose`: Sindri's managed mode (preflight, fetch, render, `OnyxServeHooks`) is for a laptop, and an unmanaged Onyx is never touched by `serve` (§A.1) |

## 3. Part B: the network (nothing changes)

Today (API spec §6): loopback only, `LoopbackGuard` (it checks the **Host** header and any browser **Origin**, not the connection's
address), one human token file per serve start, no other auth, and a request with no caller headers is a tokenless `session`.
Pod mode opens nothing: the listener stays loopback-only. `ServePort.Listen` (`ServePort.cs:35`) and `LoopbackBinding.Build`
(`LoopbackBinding.cs:40`) listen on `IPAddress.Loopback` only: no any-address listener, no pod-mode exception, no Service. A unit
test fails the build if any `Listen` in `src/Sindri` is not loopback, pod mode included. `control.json` records the loopback URL,
and `connect` forwards that port to the pod.

### B.1 The owner's rights: unchanged

- The API listener's behaviour and its guard rules stay exactly as they are. `kubectl port-forward`
  reaches the pod's loopback (the container runtime connects from inside the pod's network namespace), so the owner arrives as
  loopback with `Host: localhost`, which the guard allows. `kubectl exec -it` gives the CLI a terminal and none of the agent
  signals, so it calls as the human at a terminal and the protected verbs (`profile approve`, `recipe approve-all`, `onyx attach`)
  work as they do on a laptop; without `-t` it calls as a session and is refused (`SND-AUTH-002`), as it should be.
- The desktop app reaches it through `scripts/sindri-pod.sh connect` (Part C.3).

### B.2 Profile keys and the data volume

- **Every profile key is read from the APPROVED profile**, so an edit, and an undo, take effect when the owner approves, not when
  the file is saved. `repo-pull` reads the approved profile only.
- **Protected verbs stay human-only.** A caller is the human only by presenting `X-Sindri-Token` equal to the per-start human token
  (`auth/human.token`, 0600 in a 0700 folder on Sindri's volume); `X-Sindri-Caller: human` without it is 403 `SND-AUTH-001`, never
  downgraded. The CLI is human only at a terminal. Over raw HTTP the token is the whole gate.
- **Safety rule: nothing but Sindri's container mounts Sindri's data volume** (`/data`, which holds `auth/human.token`, the ledger
  and the Onyx token). The chart mounts it into Sindri's container and its volume init container (the same image) only; Onyx and
  Ollama have claims of their own, and a `helm template` test fails the build if any other container, init container or `emptyDir`
  mounts `/data` or a subPath of it. A mounted `/data` or a readable `auth/` anywhere else in the pod would be the owner's rights.

### B.3 Onyx on the pod's loopback: unmanaged, attached

Nothing changes in the query adapter. Onyx's `nginx` answers on `http://127.0.0.1:<onyx.port>` (§A.3), so `OnyxSettings.CheckUrl`'s
loopback rule (`localhost`, `127.*`, `[::1]`) passes as it is. The profile's section is the unmanaged shape, `context.sources.onyx`
with `url: http://127.0.0.1:<onyx.port>`, `token: <secret pointer>`, `version: v4.9.0` and `managed: false` (a managed one must be
exactly `http://127.0.0.1:<port>` and is started by `serve`; an unmanaged one is never touched, `OnyxServeHooks`). The pod needs
**the account stage only**, so the verb is small:

- `sindri onyx attach [--url http://127.0.0.1:<port>] [--adopt]` (default URL from `AW_SINDRI_ONYX_URL`, which the chart sets). A **protected
  verb** like `onyx up`: a human at a terminal only (a session or a non-terminal gets `SND-AUTH-002`, exit 1). It first **waits for
  Onyx's health route** (up to 20 minutes; `--no-wait` checks once), because right after install Onyx's images are still pulling or `api_server` is
  migrating: every 30 s it prints "Onyx is still starting: images pulling or the database migrating (<containers not answering>), <m> min"
  (the containers named from `AW_SINDRI_ONYX_PORTS`), and Ctrl-C and running it again later is safe. After 20 minutes it ends with `SND-ONYX-016`,
  exit 1: "Onyx did not answer within 20 minutes (<container> still not answering). It may still be pulling images or migrating its database.
  Run `sindri-pod.sh doctor` to see which container is not answering, then run `sindri-pod.sh connect --onyx` again; waiting again is safe." It then checks
  the release `context/onyx.lock` pins and runs the same account stage as `onyx up`
  (the owner's sign-up in the browser, Sindri's dedicated user and token, the profile section above). It **proposes** that section
  and never approves it: the install order (§C.3) runs `attach` before the one `profile approve`, so the Onyx source, the repos and
  the switches are approved once. It starts, pulls and writes nothing in Onyx's workspace, and is safe to re-run.
- **The first sign-up is Onyx's admin.** Between the pod starting and `attach`, any container of the pod could register first.
  `attach` therefore refuses with `SND-ONYX-014` when Onyx already holds an account that this setup did not create: it counts
  Onyx's users before the sign-up step; zero is fine, and one is fine only if it is the account `attach` recorded on Sindri's
  volume (`onyx-attach.json`, owner-only) at an earlier, dropped run. **Until `connect --onyx` has run, Onyx's first
  account is unclaimed:** the chart's NOTES say so and print `connect --onyx` as the step to take once `sindri-pod.sh doctor` shows Onyx ready, so do it soon after install. K-S0 (§7 item 6) checks whether Onyx can refuse further sign-ups after the first account; the chart sets it if so, and the NOTES say plainly if not.
- **`attach --adopt`: an Onyx whose account an earlier Sindri volume made** (the volume was lost, or restored from an older
  snapshot than Onyx's, which is a normal mishap). `SND-ONYX-014` is not a reason to wipe anything. On the owner's machine the command is `sindri-pod.sh connect --onyx --adopt`, which passes `--adopt` to `attach`. With `--adopt`, at a terminal,
  the owner is asked for the existing admin's email and password (not shown, never stored, in no log, file or argument, as
  `onyx up` asks for them), `attach` logs in with them, checks the account is an admin, and then runs the same steps as a first
  attach to make Sindri's own user and a **fresh token**; it records the account in `onyx-attach.json`. It never deletes or
  changes anything else in Onyx. If the owner did not make that account, they must not adopt it: the only fix is an empty Onyx
  claim, which deletes everything Onyx holds (its data, its connectors and their OAuth credentials), and the fix text says so (deploy.md has the steps, §C.5).
- **Already attached** means all three hold: the recorded token still resolves and Onyx takes it as an admin, Onyx answers its
  health route, and `/api/version` equals the release `context/onyx.lock` pins. Then `attach` says so ("Onyx is already attached
  (release v4.9.0). Nothing to do.") and exits 0, without waiting for Enter. After an Onyx upgrade (§C.5) the version differs, so it
  is **not** a no-op: it says "Updating the pinned version to v4.9.x", rewrites the profile section's `version` and proposes it for
  the next approval.
- **How it ends.** Every run ends with a last line and the next step. Attached: "Onyx is attached. Next: allow your git host with
  `sindri-pod.sh profile edit --no-approve`, add repos with `sindri-pod.sh repo add <name> <url>`, then `sindri-pod.sh approve` once." Exit codes: 0 attached or already
  attached; 1 refused or failed, with the code printed (`SND-AUTH-002`, `SND-ONYX-014`, …); 2 usage or a tool error.
- **Onyx off.** With `AW_SINDRI_ONYX_ENABLED=false` `attach` refuses with `SND-ONYX-015` ("This install runs without Onyx
  (onyx.enabled=false). Set it to true and run helm upgrade to add Onyx."), and the approve preview says business context is
  unavailable (§C.3).
- **The address printed at sign-up** is the owner's forwarded one: `connect --onyx` passes it in `AW_SINDRI_ONYX_FORWARD_URL`
  through the exec; with that unset (a plain `kubectl exec`) `attach` prints the in-pod address and says to port-forward it.
- It revives from the branch `feat/sindri-onyx-attach` (`cb225bf00`, a wip) **by cherry-picking `cb225bf00` alone onto a fresh branch
  off main, not by a rebase**: the branch stacks `87028b190` (the `network: cluster` setting, `OnyxClusterPolicy` and
  `AW_SINDRI_ONYX_HOSTS`) and K-A1's two commits (`a1c21244e`, `a1ff34780`) under it, and `cb225bf00` itself also edits cluster-name code
  (`OnyxAttachClusterNameTests`, `OnyxOrigin`, the `HttpOnyx*` adapters, `YamlOnyxProfileWriter` and its tests), so the builder keeps
  the service, the `IOnyxAccountStage` split of the account steps, the verb and its catalog row, and leaves those pieces out; the wip's
  refusal of a loopback address is reversed (attach is for the loopback Onyx here), and no network setting is read. It needs
  nothing from K-S0 or K-A1.
- If Onyx is unreachable, `context_query` answers with its code and docs hits and a note that Onyx is unavailable (the existing
  partial-answer behaviour), doctor goes warn, and nothing else stops.

### B.4 Where agents run: out of scope

Sindri listens only on its own loopback inside its pod or task, exactly as on a laptop. A caller that reaches that loopback, such as
the owner through `kubectl port-forward` or `kubectl exec`, uses it as today. Where coding agents run, how they connect, and
opening Sindri to other machines are a later spec (K-I1); this spec adds no agent container, no Service and no cluster door.

## 4. Part C: running it (context lane)

### C.1 The chart

`deploy/helm/sindri/` (new, ours), one release: a StatefulSet of one (`podManagementPolicy: Parallel`, A.3 Start order) whose pod holds Sindri, the Ollama sidecar (pinned to `OLLAMA_HOST=127.0.0.1:11434` so it never listens on the pod's address), **Onyx's containers (§A.3, rendered by `scripts/onyx-pod.mjs` into one template file, never edited by hand)** ; the volume claims (`/data`, default 50 Gi, mounted into Sindri's container and its volume init container only; `onyx-data`, default 60 Gi; `ollama-models`, default 10 Gi, mounted into Ollama only); the images by digest; the env from a Secret (`env:` pointers) and Onyx's own Secret (§A.3). **There is no Service:** nothing in the pod listens on the pod's address (§6), so there is nothing to publish; the owner's `connect` forwards the API port, and Onyx's, to the pod directly. `terminationGracePeriodSeconds: 120` (§A.1); every container's requests equal to its limits, from values (§A.3); and the probes: for Sindri a startup probe (generous, for the first volume setup), a readiness and a liveness probe, **all three `exec` probes** that run `curl -fsS --max-time 4 http://127.0.0.1:<port>/sindri/ping` inside Sindri's container (`curl` is in the image, A.1). Their timings are set, because the kubelet's default `timeoutSeconds` for an exec probe is 1, which fires before curl's own limit while a CPU-limited Sindri is busy: startup `periodSeconds: 10`, `failureThreshold: 60` (ten minutes for the first volume setup), `timeoutSeconds: 5`; readiness `periodSeconds: 10`, `timeoutSeconds: 5`, `failureThreshold: 3`; liveness `periodSeconds: 20`, `timeoutSeconds: 5`, `failureThreshold: 6` (two minutes of failures before a restart, so a throttled Sindri in the middle of an index build is not restarted). Liveness calls only `/sindri/ping`. The probe's curl runs in Sindri's container, inside its own 1 vCPU cgroup, so it competes with index builds and the Temporal child. An `httpGet` probe is impossible here: the kubelet connects to the pod's address, where nothing listens, and a probe host of `127.0.0.1` would be the node's loopback, not the pod's. For Onyx's containers liveness probes only (§A.3 Start order). The pod carries `podAnnotations`, whose defaults are the mesh opt-outs `sidecar.istio.io/inject: "false"` and `linkerd.io/inject: disabled`: there is no listener to mesh, but an injected sidecar without equal requests and limits would demote the pod to Burstable; a deployer adds the keys of any other mesh. Logs are JSON lines on stderr (`AW_LOG_LEVEL`), read with `kubectl logs -c <container>`. Telemetry export goes to `telemetry.endpoint` when set. Sindri's and Ollama's security contexts are non-root, read-only root, **all capabilities dropped**, no privilege escalation, default seccomp; Onyx's containers get the strictest context their image allows, all capabilities dropped and only the proven ones added back (§A.3, recorded per container by K-S0). **Nothing is privileged.** Values hold every deployer-specific thing: registry, namespace, the git hosts' credentials (one Secret key per host; the repos themselves are added with `sindri-pod.sh repo add`, which writes the profile, and the hosts `repo-pull` may use are a profile key, §C.2; a changed Secret needs a pod restart), storage class, resources, `sindri.port` (the API port, default 7410), `onyx.platform`, `onyx.port`, `telemetry.endpoint`, `podAnnotations`, `git.credentials` (below), `onyx.existingSecret` (A.3 Secrets) and `onyx.enabled` (default `true`; `false` leaves every Onyx container, the `onyx-data` claim and its Secret out and sets `AW_SINDRI_ONYX_ENABLED=false`, for a pod that serves code and docs only and is smaller). **The chart's values never set profile keys**: `index.graph.enabled`, `repo-pull.allowedHosts` and the rest live in the profile only and change with `sindri-pod.sh profile edit` (§C.3). The chart ships a `values.schema.json` (Kubernetes **1.29 or later**, the version the chart is tested on and the kind run is pinned to), an example values file, and a `helm lint` plus `helm template` test in CI (rendered against the example, checked for the security contexts of §A.3 and §C.1, for "no `privileged`, no host path, no Docker socket" anywhere, for "no Service", for "no container but Sindri's mounts `/data`", and for "every image is a digest"). The Kubernetes Secret for git is mounted into Sindri's container only. There is no PodDisruptionBudget, no snapshot CronJob and no NetworkPolicy in v1 (§11).

**Storage.** Kubernetes charges a container's writable layer and logs to its `ephemeral-storage` limit and the contents of an
`emptyDir` to the pod; the pod is evicted when those together pass the sum of its containers' limits, when one container passes its
own, or when one `emptyDir` passes its `sizeLimit`. So **every `emptyDir` the chart creates has a `sizeLimit`** and **every container
has an `ephemeral-storage` request equal to its limit**: `sindri` 2 Gi (layer and logs 1 Gi, its `/tmp` `emptyDir` 1 Gi), `ollama`
1 Gi, and Onyx's eight containers 12 Gi together (`nginx`, `api_server`, `background` and `inference_model_server` 2 Gi each;
`web_server`, `relational_db`, `opensearch` and `cache` 1 Gi each; nginx's four `emptyDir`s of 128 Mi each, the three log
`emptyDir`s of 512 Mi each and `background`'s `/tmp` of 256 Mi, if K-S0 shows the bot-flag command needs it; Postgres's `/dev/shm`
is memory-backed, 1 Gi, and counts as memory; K-S0 re-measures all of these). The pod's total is 2 + 1 + 12 = 15 Gi.

**The node floor** (the owner's choice of 2026-10-23: one node up front, no spike gates it). Requests equal limits, so the node must
hold them whole plus its own overhead. CPU: Sindri 1, Ollama 1, Onyx 5, and about 1 for the kubelet, OS and DaemonSets: **8 vCPU**
(Onyx's published preferred tier is 8+ vCPU and its minimum 4, docs.onyx.app "resourcing", checked 2026-10-23). Memory: Sindri 4,
Ollama 3, Onyx's eight containers 18 (25 GiB of limits), and about 3 for the kubelet's reservation and eviction threshold:
**28 GiB**. Node disk: (20 for the OS, runtime and logs, 28 for images, Onyx's about 11 GB and Sindri's and Ollama's about 3 GB, each
held twice while pulling, and 15 for the ephemeral limits above) ÷ 0.85, because the kubelet removes unused images at 85 % use:
**75 GiB**. Volumes: **120 GiB** (50 Sindri's, 60 Onyx's, 10 Ollama's), which are separate volumes the cluster creates (on EKS, EBS
volumes), not part of the node's root disk, and zonal. The recommended node of §0 (8 vCPU, 32 GiB, 100 GiB) sits just above this;
if it does not schedule, take the next size up. The chart's NOTES print this sum, and an optional `node.capacity` value makes
`helm template` fail when the sum is larger than the node the deployer says they have.

**Starting requests = limits** (the schema minimums; each is a value the deployer may raise): `sindri` 1 vCPU and 4 GiB, `ollama` 1 vCPU and 3 GiB, Onyx's containers 5 vCPU and 18 GiB together, split by container in K-O1's render (`opensearch` 2 GiB heap in a 4 GiB limit, `relational_db` 2 GiB plus its 1 GiB memory-backed `/dev/shm`, `inference_model_server` 4 GiB, `api_server` 2 GiB, `background` 3 GiB, `web_server` 1 GiB, `cache` 512 MiB, `nginx` 256 MiB: 17.75 GiB). That is 7 vCPU and 25 GiB (and 15 Gi of ephemeral storage, C.1 Storage). **Guaranteed QoS needs requests equal to limits on every container, init containers included:** the chart sets them on all, and its mesh opt-outs keep an injected sidecar without them from demoting the pod to Burstable; deploy.md says so. These are a starting point from the published figures, not measured here. K-A1's `docker run` measures Sindri and Ollama, K-S0 and the kind run measure the pod, and the numbers may move in a later revision of the chart; the floor above is what the owner provisions.

**The chart arrives in steps.** K-C1 ships Sindri and Ollama with `onyx.enabled: false` and does not wait for K-S0: it is the
first thing the kind test runs. **Milestone 1 = K-A1 + K-C2 + K-C3 + K-C1** (no Onyx): the image, the repo freshness, the owner's
connect and profile edit, and the chart. The owner's check can run then, on the node (or a small one with Onyx off). K-C1 takes its
Sindri and Ollama resource numbers from K-A1's `docker run`. K-O1 adds Onyx's rendered containers once K-S0 has reported.

Install with `helm install --timeout 30m` and **without `--wait`**: Helm returns at once and the kubelet carries on pulling about 11 GB of images in the background; `scripts/sindri-pod.sh connect` then waits for Sindri's container to be ready, which is the step that matters. `--wait` is wrong here on purpose: it waits for the pod to be Ready, and an Onyx container that loops keeps the pod from Ready while Sindri works, so it would fail an install that is fine. Sindri's readiness probe decides only the pod's Ready condition, which is what `helm --wait` and `kubectl rollout status` read, and not routing (there is no Service). The chart's NOTES say all of this, print the install command, and print `kubectl get pod -w` and `kubectl describe pod` to watch it. They then print the owner's steps of §C.3 as exact commands with this release's `--release` and `--namespace` filled in: **the first step is `scripts/sindri-pod.sh connect`** (it holds its terminal, so the steps after it run in a second one), then `doctor`, then `profile edit --no-approve`, `repo add` and `approve`. **Only when `onyx.enabled`**, the NOTES also print the `connect --onyx` step, placed after `doctor` shows Onyx ready, with the line "Onyx's first account is unclaimed until you run this: do it soon after install."; with Onyx off that step is not rendered. the floor sum (node disk and volumes separately).

**From a Secret key to the credential pointer.** A git credential is a Secret key that becomes one environment variable in
Sindri's container only, and the profile points at the variable with `env:NAME`. The values file maps one to the other:

```yaml
git:
  credentials:
    - host: git.example.com        # must also be in the profile's repo-pull.allowedHosts
      secret: sindri-git           # an existing Secret in the namespace
      key: token                   # its key
      env: GIT_TOKEN_EXAMPLE       # the variable it becomes in Sindri's container
```

and the repo is then added with `scripts/sindri-pod.sh repo add repo https://git.example.com/team/repo.git --credential env:GIT_TOKEN_EXAMPLE`.
The chart test checks that every `env:` pointer in the example profile names an `env` of the example values. A changed Secret
needs a pod restart.

**What `kubectl get pod` shows, and what it means** (chart NOTES and deploy.md; doctor says the same where Sindri is up):

| The pod shows | What it means | What to do |
|---|---|---|
| `ContainerCreating` or `Pending` for minutes | the kubelet is pulling ~11 GB of images, or a claim isn't bound | `kubectl describe pod`; wait, or fix the storage class |
| `ImagePullBackOff` | the registry, a digest, or `onyx.platform` doesn't match the nodes | `kubectl describe pod` names the image; check the platform value |
| `CrashLoopBackOff` on `sindri` | Sindri is down | `kubectl logs -c sindri` |
| `CrashLoopBackOff` on an Onyx container (say `opensearch`) | **Onyx is down and Sindri is up**: code and docs still answer, and queries carry Onyx's degraded note. The pod reads not Ready, but Sindri still answers | `kubectl logs -c opensearch`; `doctor` names the container whose port doesn't answer |
| `Running` and Ready, but Onyx misbehaves | Onyx containers have no readiness probes, so Ready says nothing about Onyx | `sindri doctor` (its Onyx rows) |
| `OOMKilled` | a container's memory limit is too small | raise that limit and `helm upgrade`: the pod rolls although it is not Ready (`podManagementPolicy: Parallel`, A.3); if it does not, `kubectl delete pod <pod>` (a normal delete, never forced) recreates it from the new template (the schema's minimum is the floor) |
| `Evicted` | node pressure, **or a container exceeded its `ephemeral-storage` limit, an `emptyDir` its `sizeLimit`, or the pod's total passed the sum of its containers' limits; each evicts the whole pod** (`kubectl describe pod` names the reason) | for node pressure, a bigger node (the pod is Guaranteed QoS, so it is the last to go); otherwise raise that container's limit if its layer or logs are the cause |

Onyx needs no values file of its own beyond `onyx.*` above: the script renders its containers from its pinned Compose file. Its admin sign-up page is on the pod's loopback only, so no other pod in the cluster can reach it; inside the pod, Sindri, Ollama and Onyx's other containers could, so the owner makes the admin account soon after install, with `connect --onyx` (§C.3), before connecting any source (until it runs, Onyx's first account is unclaimed, and the NOTES say so), and `attach` refuses an account it did not expect (B.3). The kind run records the whole footprint (CPU, memory, disk of every container) in `docs/sindri/deploy.md` so the owner can size the real cluster's node.

### C.2 The repo service (engine lane)

`repo-pull` (a Sindri service, on by default in pod mode and opt-in elsewhere like `context-docs-refresh`, every 1 minute; at most one refresh per repo in flight, moves of main while one runs are coalesced into one follow-up, and the index, map and docs refresh keep a minimum interval of 5 minutes per repo, so a busy main doesn't queue endless rebuilds on the one writer):
- for each profile repo with a `pull:` block (`{ url, credential: env:NAME }`): the first time (and again whenever the directory is missing: the service clones when `/data/repos/<name>` is absent, so deleting it is the fix for a repo that cannot be updated, `SND-REPO-002` and `-003`), `git clone --no-hardlinks` into
  `/data/repos/<name>` (the profile's `path` must be under `AW_SINDRI_REPOS_DIR` in pod mode); after that `git fetch origin
  <default branch>` and `git merge --ff-only`; a dirty tree or a non-fast-forward is a doctor row and a needs-you row, never a
  reset; the credential is passed through `GIT_ASKPASS` reading the pointer, never in a URL or an argument, and the helper answers only for the host of that repo's URL; the repo `<name>` must match `[a-z0-9][a-z0-9._-]*` and resolve under `AW_SINDRI_REPOS_DIR`; the URL must be https (v1; an ssh key would need a secret and a `known_hosts` file, a later addition) and its host in the profile's `repo-pull.allowedHosts` (profile-only: changing it is a profile change the owner approves, not a chart value), `--` precedes it on every git call, and git runs with `protocol.file.allow=never`, `http.followRedirects=false`, hooks off (`core.hooksPath=/dev/null`) and submodules off; the credential should be read-only (doctor can't check that; deploy.md says so);
- `code-index-main-move` then sees HEAD move and indexes, and the map and docs refresh follow, as on a laptop. The freshness number is the map's lag behind main, shown in doctor and as `lagSeconds` in the `stale` note a caller sees, and exported as a metric (telemetry export is on in pod mode, off by default elsewhere); a lag over 15 minutes is a doctor warning.

v1 pulls and indexes each repo's default branch only: work on a feature branch gets main's map, which deploy.md says.

`sindri repo add` in pod mode takes `--url`, `--name` and the credential pointer instead of an existing path, and writes the `pull:` block to the profile file; it clones nothing. **`repo-pull` reads the approved profile only** (the repos, their `pull:` blocks and `repo-pull.allowedHosts`), so a repo that `repo add` recorded is cloned after the approval, not when it is added; `repo add` itself, an owner action, checks the host against the live file so the owner can add before approving.

### C.3 The owner's steps

| Step | How | One click? |
|---|---|---|
| Install | create a namespace labelled `pod-security.kubernetes.io/enforce=baseline` (or stricter, §A.3; the chart's NOTES name the level the rendered containers need), then `helm install --timeout 30m` (no `--wait`, §C.1) the one release with the deployer's values. The kubelet pulls about 11 GB of Onyx images, so the pod takes minutes to become Ready; Sindri is first in the pod, so it starts as soon as its own image is there, and no Onyx container gates it | one command each |
| Open the app on the pod | `scripts/sindri-pod.sh connect` (flags below): `kubectl port-forward` to the pod's loopback API port, `kubectl exec … sh -c` reading the pod's `control.json` and `auth/human.token` (no new verb; built in K-C3) for a fresh human token **and the pod's current pid and lease epoch** (the app accepts a `control.json` only when its `Pid` and `Epoch` match `/sindri/ping`, which on loopback still carries the pid), a local mirror home (`control.json` with the forwarded loopback URL, pid, epoch, the token file, all 0600) and `sindri-ui` started on it. The app needs no change: it still reads a loopback URL and a token from a home. At the start it waits (up to 10 minutes, printing the container's state every 15 s) for the `sindri` container to be ready. When the port-forward drops or the pod restarts (a new epoch and a new human token), the script retries and rewrites the mirror home, and the app shows its existing "control plane not running" state meanwhile, while the script prints "pod restarting, reconnecting (attempt n of 30)" and gives up after 5 minutes with exit 3. The mirror home is removed on exit, Ctrl-C included (a trap; a crash leaves it under `$TMPDIR`, 0700, and the next run removes stale ones). Prerequisites: `kubectl` with exec and port-forward rights on the namespace, and `sindri-ui` installed (`sindri-app`); `connect --check` reports each one. Each failure has one line and exit 2: kubectl missing or no rights (names the verb refused), Sindri's container not running (names its state and says `kubectl logs <pod> -c sindri`; the **pod's** Ready state is not checked, because an Onyx container that loops is normal and Sindri still works, so with Onyx not answering `connect` carries on and prints "Onyx: not answering, see sindri doctor"), the local port taken (names it and `--port`), `sindri-ui` not found (names `sindri-app`). It prints "connected: <pod> at <url>; Ctrl-C to stop" on success. It holds this terminal until Ctrl-C, so the steps after it run in a second terminal. Its own exit codes: 0 a normal end (Ctrl-C), 2 a prerequisite or start failure, 3 gave up reconnecting (the table below lists every verb's) | one command |
| Approve the profile, recipes | in the app (its existing typed confirmations), or at a terminal `scripts/sindri-pod.sh approve` (a thin wrapper of `kubectl exec -it <pod> -c sindri -- sindri profile approve`). **Once, after `connect --onyx` and `repo add`**, so the one preview shows the Onyx source and the repos. `repo-pull` and every profile key read the approved profile only (§B.2), so nothing is cloned and no key takes effect before this step | one click and the typed word, as today |
| Change a profile key (`repo-pull.allowedHosts`, `index.graph.enabled`) | `scripts/sindri-pod.sh profile edit [--no-approve]` (K-C3). The profile is a file on the pod's volume and the image has no editor and a read-only root, so this is the only way to change or undo a key. **When the pod has no profile yet (a fresh install, Onyx on or off), it first runs `sindri profile init` in the pod** (which writes the default profile and refuses when one exists) and opens that. It copies the profile to a temporary file on your machine (`kubectl exec … cat`, remembering the pod's copy as it was), opens `$EDITOR` (`vi` if unset), copies the result back to a temporary path in the pod and checks it there with `sindri profile validate --file <path>` (a small addition K-C3 makes; exit 2 means invalid, and the editor reopens with the error; leaving it invalid aborts with exit 3 and keeps your file), replaces the live file only if the pod's copy did not change meanwhile, and then runs `sindri profile approve` in the pod (`kubectl exec -it`) so the preview and the typed word are yours. `--no-approve` saves the file and stops (the first install, where the one approval comes at the end). **Every profile key is read from the APPROVED profile (§B.2), so an edit, and an undo, take effect when you approve, not when the file is saved; until then the old value stays in force.** If the pod's copy changed meanwhile (a `repo add`, an `onyx attach` or another edit), nothing is replaced and it prints "The profile on the pod changed while you were editing (<the top-level keys that differ>). Nothing was replaced; your edit is kept at <path>. Run profile edit again and make the change on top of the new copy." Exit codes in the table below | an editor and the typed word |
| Add a repo | `scripts/sindri-pod.sh repo add <name> <url> [--credential env:NAME]`, a thin wrapper of `kubectl exec -it <pod> -c sindri -- sindri repo add --url <url> --name <name> [--credential env:NAME]` (K-C3; in pod mode the verb takes `--url` instead of a path, §C.2). It records the repo in the profile file and refuses an unlisted host (`SND-REPO-005`), so run `profile edit --no-approve` first. **It clones nothing:** the first clone runs after the approval, because `repo-pull` reads the approved profile. It prints "<name> recorded. It is cloned after you approve: sindri-pod.sh approve." | one command per repo |
| Check the pod | `scripts/sindri-pod.sh doctor` (a thin wrapper of `kubectl exec -it <pod> -c sindri -- sindri doctor`). The Onyx rows read "not set up" or warn until Onyx's containers answer, which takes minutes after install | one command |
| Onyx | **After `sindri-pod.sh doctor` shows Onyx ready** (before that it is still pulling or migrating), stop `connect` with Ctrl-C and run `sindri-pod.sh connect --onyx [--adopt]`, which does everything `connect` does (the app opens too) and forwards **Onyx's port** from the pod to the owner's machine **on the same local port as the pod's `onyx.port`** (8081 by default, read from the pod's `AW_SINDRI_ONYX_URL`), because Onyx's `WEB_DOMAIN` is `http://127.0.0.1:<onyx.port>`, so its sign-up redirects and connector OAuth callbacks come back to that address. A taken local port is one line, "local port 8081 is in use: stop what uses it, then run this again" (exit 2). `AW_SINDRI_POD_ONYX_PORT` overrides the local port and prints a warning ("sign-up redirects and connector OAuth may land on port 8081, not <chosen>"); K-S0 tests sign-up through a different local port, and the OAuth round trip is the owner's check (§7). It **prints the forwarded address before it runs `attach`** ("Onyx's page: http://127.0.0.1:8081"), then runs `kubectl exec -it <pod> -c sindri -- sindri onyx attach` (with `--adopt` when you gave it) at the same terminal with that address in `AW_SINDRI_ONYX_FORWARD_URL`. `attach` checks Onyx answers and is the pinned release, then runs the account steps; at the sign-up step it prints the forwarded address, the owner signs up in the browser and presses Enter. `attach` pulls and starts nothing (the kubelet did that at install). If Onyx is nonetheless still starting, `attach` waits for its health route for up to 20 minutes and prints "Onyx is still starting: images pulling or the database migrating" with the containers not answering every 30 s (Ctrl-C and running it again later is safe); the timeout is `SND-ONYX-016` (B.3), shown with `attach exited 1`, and the app stays connected. Once Onyx is ready it takes seconds. `attach`'s failure codes (for example `SND-ONYX-014`, an account already exists that this setup didn't make) are printed unchanged, followed by `attach exited <n>` on stderr (the script's own exit codes are 0, 2 and 3, so the two never mix), and **the app stays connected**; Ctrl-C during `attach` stops that command only. An already-attached, healthy Onyx makes `attach` a no-op that says so, exit 0. A dropped exec mid sign-up (a laptop that slept) is fixed by running the same command again; `attach` picks up where it was. It ends with attach's last line and the next step (§B.3). With Onyx off (`onyx.enabled: false`) it refuses at once: "Onyx is off in this install (onyx.enabled=false); use connect.", exit 2 | one command, one browser sign-up, one Enter |
| Connect sources | in Onyx's page (Linear, Slack, GitHub, docs), the connector checklist `onyx attach` prints. It warns against internal URLs (`localhost`, `127.0.0.0/8`, `::1`, the pod's address, `*.svc`): Onyx's web connector reaches this pod's loopback (§6). | the owner's OAuth, in Onyx |

**Flags every verb takes.** `connect` (and `connect --onyx`), `profile edit`, `repo add`, `approve` and `doctor` each take `--context C` and `--namespace N` (kubectl's own defaults when omitted) and name the pod with `--pod P`, or with `--release R` (the pod is `<R>-0`, the StatefulSet's name for its one pod); with neither, the pod is `sindri-0`. No owner command hard-codes a pod name except as that default: this spec writes `<pod>` for it.

**Exit codes, per verb.** The numbers are per verb: only 0 and 2 mean the same everywhere (0 done, 2 cannot start: usage, a prerequisite, or the pod unreachable); 3 differs, and the bash test asserts each row.

| Verb | 0 | 1 | 2 | 3 | 4 |
|---|---|---|---|---|---|
| `connect`, `connect --onyx` | a normal end (Ctrl-C) | not used (an `attach` failure is printed as `attach exited <n>` and the app stays connected) | a prerequisite or start failure, or Onyx off with `--onyx` | gave up reconnecting | not used |
| `profile edit` | changed and approved; or unchanged (nothing replaced, nothing to approve); or `--no-approve` done | approve refused or declined | usage, or the pod unreachable | an invalid edit aborted (your file is kept) | conflict: the pod's copy changed meanwhile (your edit is kept) |
| `repo add`, `approve`, `doctor` | the wrapped command's own code, passed through | | | | |

For `repo add`, `approve` and `doctor` the script passes the wrapped command's own exit code through, except that it returns 2 for its own failures (usage, the pod unreachable).

The scripts' terminal output follows `NO_COLOR` and prints no spinner or colour when stdout is not a terminal; the bash tests
check both for `connect`, `connect --onyx`, `profile edit` and the lines `attach` prints.

**The first install, in order, as exact commands** (`<release>` and `<ns>` are yours; add `--context` where you use more than one cluster; times from the kind run go into deploy.md). `connect` holds its terminal until Ctrl-C, so the steps marked terminal 2 run in a second terminal.

1. Terminal 1, once: create the namespace (labelled `pod-security.kubernetes.io/enforce=baseline`), create the Secrets your git hosts need (`kubectl create secret generic <name> --from-literal=<KEY>=…`, mapped to environment variables by `git.credentials`, C.1), then `helm install <release> deploy/helm/sindri --namespace <ns> --timeout 30m -f values.yaml`. No `--wait`: it returns at once and the image pull carries on.
2. Terminal 1: `scripts/sindri-pod.sh connect --release <release> --namespace <ns>`. It waits for Sindri's container, opens the app and holds the terminal.
3. Terminal 2: `scripts/sindri-pod.sh doctor --release <release> --namespace <ns>`, again every few minutes, until the Onyx rows are ok (skip this and the next step when Onyx is off).
4. Terminal 1: Ctrl-C, then `scripts/sindri-pod.sh connect --onyx --release <release> --namespace <ns>`; sign up in the browser and press Enter. Do it soon after install: Onyx's first account is unclaimed until it runs.
5. Terminal 2: `scripts/sindri-pod.sh profile edit --no-approve --release <release> --namespace <ns>`. It creates the default profile when there is none; set `repo-pull.allowedHosts`.
6. Terminal 2, per repo: `scripts/sindri-pod.sh repo add <name> <url> --credential env:<NAME> --release <release> --namespace <ns>`. This records the repo and clones nothing.
7. Terminal 2: `scripts/sindri-pod.sh approve --release <release> --namespace <ns>`, once, and type the word. The first clone and index follow (minutes per repo; done when doctor's map row for the repo is ok and its lag row shows a number).
8. Check: `scripts/sindri-pod.sh doctor` shows the repos and the map. Then, when wanted, connect Onyx's sources.

`attach` writes the Onyx source into the profile file, so steps 5 and 6 come after step 4; a `profile edit` that overlaps an `attach` or a `repo add` ends with exit 4 and loses nothing.

**The approve preview** lists the repos, the `repo-pull` hosts and the Onyx source it approves (with Onyx off: "Chat and tickets are unavailable: Onyx is off in this install."). After an undo it says what was withdrawn: a repo removed from the profile adds "<repo> is no longer served." A withdrawal takes effect at approve, like every change.

### C.4 Network rules and who can read what

There is no NetworkPolicy on the namespace by default, and the chart ships none in v1 (a reversible default), and none is needed for
ingress: Sindri's API, Temporal, Ollama and every Onyx container listen on the pod's loopback only, and nothing listens on the pod's
address, so no pod outside this one can connect to anything in it (§6). The owner's routes are `kubectl port-forward` and `exec`.
Those rights are the owner's power (they reach the human token, the ledger and Onyx's admin page), and so are volume-snapshot read
rights and the right to create a pod or claim in the namespace (they reach the human token, the ledger, Onyx's database with the
connector OAuth credentials, and Sindri's Onyx token). **The namespace is the owner's alone**; deploy.md says so in its first section,
next to the costs of the layout, and says to use an encrypted StorageClass where the cluster has one. A deployer who wants egress
rules lists the git and forge hosts, Onyx's registries and connector services, and the model hub (§A.3); a NetworkPolicy works per
pod, so those rules cover Sindri and Onyx alike.

### C.5 Upgrading and getting back

`docs/sindri/deploy.md` gains: upgrading Sindri (a new image digest in the values, `helm upgrade`, the pod restarts and takes the
lease; Onyx's containers restart with the pod, so Onyx answers degraded for a few minutes; the only downgrade is the previous
digest plus, if state moved, a snapshot restore), upgrading Onyx (**bump the pin in `context/onyx.lock`, `onyx-lock.sh verify`,
refresh `deploy/onyx/docker-compose.yml`, run `node scripts/onyx-pod.mjs`, read the diff, build a new Sindri image that embeds
the new lock, `helm upgrade`, then `sindri onyx attach` again so the profile's pinned version follows**; doctor's `/api/version`
row shows a mismatch until it does; Onyx's database migrations run forward at `api_server` start, so the only way back is a
snapshot restore), and the manual backup and restore of K7 (snapshot both claims at the same moment, restore both from the same pair, so the map's Onyx cursor and Onyx's database match; the heavy test runs this restore once). The procedure includes exporting Onyx's Secret with the snapshots (K7) and restoring it first; the heavy test restores into a fresh namespace with the exported Secret.

deploy.md also has **"Starting Onyx over"**, the fix text of `SND-ONYX-014` when you did not make the account, with a warning that it deletes everything Onyx holds (its data, its accounts, its connectors and their OAuth credentials): `helm uninstall <release>` (this removes the pod; the claims and Onyx's kept Secret stay), `kubectl delete pvc <the onyx-data claim>` (`kubectl get pvc` names it), and, only if you also want fresh passwords, `kubectl delete secret <Onyx's Secret>`; then `helm install` again and `connect --onyx` for the new first account.

## 5. Part D: the whole map on open, a query zooms in

As written in `2026-10-23-sindri-context-whole-map-design.md`: the page shows the whole map the moment it opens, a query zooms the
camera into it, `GET /sindri/context/map/whole` with a node and an area level. In the pod the page is the owner's (through the
connected app). Nothing in Part D is specific to the pod; the `context_map_whole` tool that spec adds is offered to any local
session, as on a laptop.

## 6. Security

- **Loopback only.** Sindri's API is a loopback listener, as on a laptop, and nothing listens on the pod's address, so nobody
  outside the pod can reach it. The protected verbs need the per-start human token, which lives in Sindri's volume (`auth/`,
  owner-only) and which no other container mounts (B.2).
- **Nothing is privileged**, there is no Docker daemon or socket, and no pod host path. The cost of dropping Docker's private
  network: **the pod's loopback is common to every container in it**, so the boundary inside the pod is the pod. Ollama and every
  Onyx container (api_server, background workers, the model servers, the web server, Postgres, OpenSearch, Redis, nginx) can reach
  Sindri's API on `127.0.0.1` with no token, as a session (a human-only action still needs the human token they cannot read); the
  Temporal dev server's unauthenticated frontend; Redis, which Onyx's Compose file leaves without a password; and Onyx's web, API and
  nginx (including its first sign-up). Onyx's Postgres and OpenSearch need their passwords, which are in Onyx's Secret, mounted into
  Onyx's containers only. The sharpest case remains Onyx's connectors (`background`) and web connector, which fetch addresses the owner
  gives and whose results Onyx stores: a connector pointed at `http://127.0.0.1:7410/…` would read Sindri's own API as a session.
  **Accepted for v1** under the honest-mistake model (pinned prebuilt images, connectors chosen by the owner); mitigations: the
  connector checklist says to give the web connector only public URLs (and warns against `localhost`, `127.0.0.0/8`, `::1`, the pod's
  address and `*.svc`), Temporal runs headless in pod mode (no UI port), K-S0 records whether Onyx's own web-connector guard already
  blocks loopback, including through a redirect to loopback and through a DNS name that resolves to `127.0.0.1`, and a later spec
  could put Sindri's API behind a Unix socket or a token (K-I1). The alternative, noted: Onyx in its own pod, which
  `OnyxSettings.CheckUrl`'s loopback rule forbids today.
- **What listens on the pod's address**: nothing. Every Onyx container, Temporal (`--ip 127.0.0.1`), Ollama (`OLLAMA_HOST`) and
  Sindri's API are loopback-only. Whether each Onyx image honours its loopback setting is **unverified until K-S0**, which reads the
  pod's listeners once (A.3) and scans the pod's address from another namespace, and must find nothing.
- **Secrets** live in Kubernetes Secrets and reach Sindri as `env:` pointers; the image and the chart's values hold none; Onyx's
  secrets are a chart-made Secret mounted into Onyx's containers only; the Onyx token Sindri makes stays on the volume, owner-only.
- **Onyx's first account is unclaimed until `connect --onyx` has run.** Any container of the pod could register first. `attach`
  refuses an account it did not expect (`SND-ONYX-014`), and the chart's NOTES say that the step should follow soon after install,
  once doctor shows Onyx ready.
- **Onyx's Secret outlives an uninstall** (`helm.sh/resource-policy: keep`) and is part of the backup (K7), because it holds the key to
  the stored connector credentials; `onyx.existingSecret` lets the owner supply it (A.3 Secrets).
- **Snapshots and claims are the secrets store.** Anyone who can create a pod or a claim in the namespace, or read a volume
  snapshot, reads the human token, the ledger, Onyx's database (the connector OAuth credentials) and Sindri's Onyx token. The
  namespace is the owner's alone, snapshots are a manual procedure the owner runs, and the StorageClass should be encrypted
  where the cluster has one (§C.4).
- **Supply chain.** Every kubelet image by digest: our own, the base images and Ollama in `deploy/images.lock`, Onyx's in
  `context/onyx.lock`; our own image built from the repo by CI, both locks checked in CI, and the rendered Onyx template checked
  against them by `onyx-pod.mjs --check`; a `helm template` test asserts every image in the chart is a digest from a lock. Model
  weights are pinned too (Hugging Face revisions, Ollama's digest) and verified before Sindri serves queries.

## 7. Testing

- **Unit:** pod-mode settings (unset is the desktop behaviour: a golden of the settings, doctor output and `serve`'s start log with the variable unset), the lease's stable host id, **an architecture test that every `Listen` in `src/Sindri` is loopback, pod mode included**, `repo-pull`'s decisions (clone, fast-forward, dirty, diverged), `onyx-pod.mjs` (its override table, the refusal of a fallback password, the per-platform digest choice).
- **Integration:** `onyx attach` against a fake Onyx (healthy, wrong version, unreachable, refused for a session, a foreign first account `SND-ONYX-014`, `--adopt` with the existing admin, still starting then healthy (it waits and prints the state), never healthy (`SND-ONYX-016`), already attached, a version mismatch that updates, Onyx off `SND-ONYX-015`); the bash tests of `sindri-pod.sh` with a fake `kubectl` (`connect` with the Onyx container looping and the `sindri` container running connects and prints "Onyx: not answering, see sindri doctor"; `profile edit` with no profile yet runs `profile init`, with a fake `$EDITOR`, an invalid edit (exit 3), a changed-meanwhile file (exit 4; the message names what changed and the kept path), an unchanged file (exit 0) and `--no-approve`; `repo add`, `approve` and `doctor` build the right exec line; `--release` and `--pod` pick the pod; `connect --onyx` with Onyx still starting waits and prints the state every 30 s on a fake clock, and `--adopt` is passed on; every row of the exit-code table of §C.3); `onyx up` refused in pod mode (`SND-ONYX-013`); the chart's `helm template` test (nothing privileged, every image a digest from a lock, `podManagementPolicy: Parallel`, the Onyx Secret's `keep` annotation and `onyx.existingSecret` (no Secret made, no `lookup`), no `Service`, exec probes, **no container, init container or `emptyDir` but Sindri's and its init container mounts `/data` or a subPath of it**, a `sizeLimit` on every `emptyDir` and an `ephemeral-storage` request equal to its limit on every container (the pod's sum is 15 Gi), the Onyx Secret checked by keys because `lookup` returns nothing under `template`); `repo-pull` against a local bare repo; `repo-pull` refuses a `file://` URL, a bad name and an unlisted host.

- **Spike (K-S0), time-boxed to 2 working days, before K-O1:** on kind with a pinned node image (Kubernetes 1.29 or later) and a namespace labelled `pod-security.kubernetes.io/enforce=baseline`, a hand-written pod with Sindri's image and the Onyx containers at the lock's digests, no privilege anywhere. It fills this fixed checklist into `docs/sindri/deploy-spike.md`, one line per item (result, or "failed" with the evidence):
  1. every Onyx container reaches the others on `127.0.0.1` with the host variables of §A.3, and the pod settles with **no start ordering** (A.3 Start order); **time-to-Sindri-up on a cold node** (`/sindri/ping` answers before the last Onyx image finishes pulling, with Sindri first in the spec and the init container on Sindri's image) is recorded;
  2. nginx reaches its upstreams with `ONYX_BACKEND_API_HOST` and `ONYX_WEB_SERVER_HOST` set to `127.0.0.1` (`hostAliases` only if that fails), the `sed` that moves its listen port works on the copied template with the pinned files unedited, and its writable directories suffice (A.3 nginx);
  3. OpenSearch bound to loopback starts with `bootstrap.memory_lock=false`, its bootstrap checks only warn, and what the warning costs, and the TCP liveness probe on `9200` (HTTPS, demo certificate) works;
  4. **per container: non-root or not, capabilities needed, and the Pod Security level that results**;
  5. the pod's listeners read once (A.3: `/proc/net/tcp` in the Sindri container, `temporal`'s excluded) and a scan of the pod's address from another namespace, which must find nothing; the port map of A.3 matches;
  6. v4.9.0 starts with `FILE_STORE_BACKEND=postgres` and makes no S3 call at startup, whether `S3_*` and `CODE_INTERPRETER_*` can be left out, and whether v4.9.0 has a setting that closes sign-up after the first account (invite-only or closed registration), with a check from another container that a second account cannot be created: the chart sets it if there is one; if there is none, that is a finding, not a skip: the install notes say that any container of the pod could register on Onyx's own page, and the owner decides (the stop rule below does not apply);
  7. where each model server's weights live, whether they download at start, the hosts contacted at start, and the Hugging Face revision setting;
  8. each `subPath` directory's owner and what the init container must do; Postgres with a memory `/dev/shm`;
  9. the web server's loopback setting (`HOSTNAME`), and whether Onyx's web connector refuses loopback addresses, **a redirect to loopback (a public host that redirects) and a DNS name that resolves to `127.0.0.1`**;
  10. Onyx's pages and `/api/version` answering from Sindri's container at `http://127.0.0.1:<onyx.port>` and through a bare port-forward at the same local port; **sign-up through a port-forward at a different local port** (`WEB_DOMAIN` holds `onyx.port`; the real OAuth round trip needs an OAuth app and a connector account that a lane does not have, so it moves to the owner check below; some providers refuse a `127.0.0.1` redirect, and if so the checklist prefers token-based connectors). The attach end to end is K-O2's proof and the kind run's, not this item;
  11. `bwrap` under the default seccomp profile (expected: graph off); the pinned `temporal`'s headless flag from `temporal server start-dev --help` (A2f: `--headless` with `--ui-port` omitted still takes `--http-port`; this needs nothing from K-O2);
  12. the pod's measured footprint (requests = limits per container, and the re-measured Onyx memory for the sizing set) **against the node floor and starting numbers of C.1 (7 vCPU and 25 GiB requested; the floor is 8 vCPU and 28 GiB)**: the report says where the measured use is above or below each starting value. It records; it does **not** gate the node, which the owner has chosen. And a pod delete with Postgres under write load.

  **What K-S0 no longer decides.** The node size: the owner picks the node up front. If the measurement shows a container above its starting limit, the chart's value is raised in K-O1's PR; if it shows the floor too small, that is an amendment here, not a delay.

  **Overrun rule.** At the end of the second working day K-S0 reports what is done and what is not, and the owner decides whether to extend the box. If not, each open item becomes a named acceptance check of K-O1 instead of a delay.

  **Stop rule.** If an Onyx image cannot run without a privileged container or cannot be kept on loopback, or no file store without S3 exists, K-S0 **stops**, K-O1 does not start, and the owner is told that Onyx stays off for v1 (`onyx.enabled: false`, code and docs only). No workaround that contradicts q73. The report is `docs/sindri/deploy-spike.md`; its results are summarized in `docs/sindri/deploy.md`; where an item fails but isn't a stop, an amendment of this spec comes before K-O1.
- **Heavy (nightly, and by hand per PR):** the image built and run (`docker run` with a volume: serve up, Temporal up, doctor ok, a restart takes the lease at once; a restore of a snapshot); the chart on a local kind cluster (`deploy/test/kind-up.sh`): `helm install --timeout 30m` (no `--wait`) into a namespace labelled `pod-security.kubernetes.io/enforce=baseline`, `connect` waiting for Sindri's container, Sindri's readiness and Onyx's liveness probes passing (Onyx has no readiness probes, so "ready" says nothing about it; doctor's Onyx rows are asserted instead), time-to-Sindri-up on a cold node recorded, `onyx-pod.mjs --check` clean, `sindri-pod.sh connect` opens the app, `connect --onyx` runs `attach` to a healthy state, **Onyx's port answers from Sindri's container on `127.0.0.1` and through a bare port-forward**, **no Service exists, and a pod in another namespace cannot connect to the pod's address on the API port or any other (the listener read and the scan find nothing)**, no container but Sindri's can list `/data`, **breaking an Onyx env var, running `helm upgrade` with the fix and seeing the pod roll with no manual delete** (A.3 Start order), an uninstall and a reinstall on the kept claims with Onyx still starting (the kept Secret), the restart-to-first-answer time (target under 2 minutes) and the footprint are recorded, **killing `opensearch` leaves `context_query` answering with the Onyx-down degraded answer**, and **`kubectl port-forward` reaches the API on `127.0.0.1` with the pod's mesh opt-outs on, and `kubectl exec -it <pod> -c sindri -- sindri doctor` works** (a mesh sidecar injected anyway can change how loopback ports behave under port-forward; the opt-outs are the mitigation). K-C1's kind test runs with `onyx.enabled: false` first; K-O1 adds the run with Onyx.
- **Owner check, once, on the real cluster:** the namespace's Pod Security level and the node's settings (§A.3) on its node image, install, connect, `connect --onyx`, approve, connect one source (**with one real OAuth round trip for a connector through the forwarded port**, which K-S0 cannot test), and look at the map of a repo in the app.

## 8. Errors and settings

| Code | Meaning | Fix text (short) | Phase |
|---|---|---|---|
| `SND-ONYX-013` | `onyx up` or `down` in pod mode (the highest code on main is `SND-ONYX-012`; K-O2 re-checks at merge and takes the next free number if another lane has taken 013) | "Onyx is part of this pod. Run `sindri-pod.sh connect --onyx`." | K-O2 |
| `SND-ONYX-014` | `onyx attach` finds an Onyx account that this Sindri did not create (the first account is Onyx's admin) | "This Onyx has an admin account that this Sindri did not create. If you made it, for example before this volume was lost or restored, run `sindri-pod.sh connect --onyx --adopt` (it passes `--adopt` to `sindri onyx attach`) and sign in with it. If you did not make it, someone else got there first: the only fix is to start from an empty Onyx claim, which deletes everything Onyx holds (its data, its connectors and their OAuth credentials); deploy.md, "Starting Onyx over", has the steps (uninstall, delete the claim, install)." | K-O2 |
| `SND-ONYX-015` | `onyx attach` where Onyx is off (`AW_SINDRI_ONYX_ENABLED=false`) | "This install runs without Onyx (onyx.enabled=false). Set it to true and run helm upgrade to add Onyx." | K-O2 |
| `SND-ONYX-016` | `onyx attach` waited 20 minutes and Onyx's health route never answered (exit 1) | "Onyx did not answer within 20 minutes (<container> still not answering). It may still be pulling images or migrating its database. Run `sindri-pod.sh doctor` to see which container is not answering, then run `sindri-pod.sh connect --onyx` again; waiting again is safe." | K-O2 |
| `SND-REPO-001` | the first clone failed | names the git error and the URL's host | K-C2 |
| `SND-REPO-002` | the checkout has local changes | "Sindri never resets a repo. Clean /data/repos/<name> by hand with kubectl exec, or delete that directory the same way: repo-pull clones it again at its next run, within a minute." | K-C2 |
| `SND-REPO-003` | main moved in a way that isn't a fast-forward | "Upstream was rewritten. Delete /data/repos/<name> with kubectl exec: repo-pull clones it again at its next run, within a minute." | K-C2 |
| `SND-REPO-004` | the credential pointer is missing or empty | names the variable | K-C2 |
| `SND-REPO-005` | the URL, its host or the repo name is refused (not https, not an allowed host, a bad name) | names which rule | K-C2 |

Existing codes reused: `SND-CONTEXT-010` (Onyx down, degraded answer), `SND-AUTH-001` (a human claim without the token), `SND-AUTH-002` (a protected verb without a terminal). Each new code gets its fix text in `SindriErrors.cs` and
a row in the API spec's §9. Part D owns `SND-CONTEXT-200..209` (its §2.4).

**Notes a caller of the context tools sees while a repo is fetched or behind** (K-C2 writes them; their texts, once).

| Where | Text |
|---|---|
| note `setting up` (first clone or first build running) | "<repo> is being set up: its code is being fetched and its map built for the first time. Retry in a few minutes." |
| note `stale` | "The map for <repo> is <lagSeconds> seconds behind the main branch." |
| note `not updated` | "<repo> is not being updated, so its latest changes are missing from this answer. Ask the owner of this Sindri." |
| note `nothing found` | "Nothing found in code and docs." |

| Variable | Set by | Default | Meaning | Phase |
|---|---|---|---|---|
| `AW_SINDRI_DEPLOY` | the image (`ENV`) | unset (desktop) | `pod` turns pod mode on; every pod-mode behaviour keys on it | K-A1 |
| `AW_SINDRI_REPOS_DIR` | the chart | `/data/repos` in pod mode | where `repo-pull` clones | K-C2 |
| `AW_SINDRI_ONYX_URL` | the chart | unset | the default URL of `sindri onyx attach` (`http://127.0.0.1:<onyx.port>`) | K-O2 |
| `AW_SINDRI_ONYX_PORTS` | the chart (**K-O1** writes it, in `onyx-pod.mjs`'s render) | unset | `name:port` pairs of Onyx's containers, which doctor opens (**K-O2** reads it) | K-O1 writes, K-O2 reads |
| `AW_SINDRI_ONYX_ENABLED` | the chart, from `onyx.enabled` | unset (read as true) | `false` makes doctor, `attach` and the approve preview say Onyx is off in this install (`SND-ONYX-015`) | K-O2 |
| `AW_STATE_DIR`, `AW_SINDRI_HOME`, `HOME` | the image (`ENV`) | `/data/state`, `/data/state/sindri-app`, `/data/home` | where state lives (a `kubectl exec` doesn't run `start.sh`) | K-A1 |
| `AW_SINDRI_PORT` | the chart, from `sindri.port` | 7410 | the API port (exists); the exec probes use it | existing |
| `OTEL_EXPORTER_OTLP_ENDPOINT` | the chart, from `telemetry.endpoint` | unset | where pod mode exports telemetry | K-A1 |
| `AW_SINDRI_POD_ONYX_PORT` | the owner's shell, read by `sindri-pod.sh` | `onyx.port` (8081) | overrides the local port `connect --onyx` forwards Onyx's page to (by default the same as `onyx.port`); using it prints a warning that sign-up redirects and connector OAuth may land on `onyx.port` | K-C3 |
| `AW_SINDRI_ONYX_FORWARD_URL` | `sindri-pod.sh`, through the exec | unset | the forwarded address `onyx attach` prints at sign-up | K-O2 |

All the above go in `.env.example`.

| Profile key | Default | Meaning | Phase |
|---|---|---|---|
| `repos[].pull` | none | `{ url, credential: env:NAME }`, written by `repo add --url` | K-C2 |
| `repo-pull.allowedHosts` | none | the git hosts `repo-pull` may clone from; **profile-only** (changing it needs approval, it is not a chart value) | K-C2 |
| `index.graph.enabled` | true | false where `bwrap` can't start (§A.2); a profile key only | existing |
| `deploy` | read-only echo | pod or desktop, for doctor | K-A1 |
| `context.sources.onyx` | written by `onyx attach` | `{ url: http://127.0.0.1:<port>, token, version, managed: false }`, an existing key | existing |

Profile keys change only through `sindri-pod.sh profile edit` (§C.3); the chart's values never set one. Every profile key is read from the approved profile (§B.2), so a change takes effect at approve. Each phase adds its keys to `docs/csharp/sindri-profile.md` (a bash test checks every key is named there).

## 9. Phases and parallel plan (for /specToProvenPR)

| Phase | Lane | What | Needs |
|---|---|---|---|
| **K-S0 spike** | context | the kind spike of §7, **time-boxed to 2 working days** with a stop rule, run as a fixed checklist; it writes `docs/sindri/deploy-spike.md` (one line per checklist item; its results are summarized in deploy.md), reports at the end of day 2 if not done (§7 overrun rule); plus `deploy/test/node-probe.sh`, a one-pod check of the node settings the owner runs on the real cluster right after; an amendment here if an item fails | K-A1's image (a local build) |
| **K-A1 image** | context | the Dockerfile (no Docker CLI; `curl` for the exec probes), the CI workflow that builds it for both platforms and pushes it to the configured registry (the build stage needs clang and zlib for NativeAOT) and prints the digest for the values, the start script, `deploy/images.lock` (the kubelet's own images: base images and Ollama; no Docker or Onyx rows) and its check, the heavy `docker run` proof, pod mode's switch (A2a) and the lease restart check (A2b, a test only) | nothing |
| **K-O1 Onyx siblings** | context | `deploy/test/listeners.sh` (A.3), the Onyx Secret's `keep` annotation and `onyx.existingSecret`, `scripts/onyx-pod.mjs` and its inputs under `deploy/onyx/` (Compose copy, `env.template`, nginx templates, `pod-overrides.yaml`, each sha-checked against the lock), `context/onyx-sizing.json` (only the values that carry over to a pod, §A.3) used by `OnyxConfig` (and the C# drift test), the nginx `sed`, **`AW_SINDRI_ONYX_PORTS` (K-O1 writes it)**, the generated `onyx-containers.yaml` of the chart (containers with their `ephemeral-storage` limits from C.1 Storage, the init container for volume directories, volumes, every `emptyDir` with a `sizeLimit`, the nginx ConfigMap, the Secret), `--check` in CI and a bash test (added to AGENTS.md's list), the `helm template` checks that nothing is privileged and every image is a lock digest, and the `deps.yaml` file edges for `scripts/onyx-pod.mjs` (to `context/onyx.lock`, the Compose copy and the sizing file) | **K-S0's report** (the script skeleton, the sha checks and `--check` can start earlier; the override table waits for the report), K-C1 for the chart skeleton |
| **K-O2 onyx attach** | api, or any lane with room | revive `sindri onyx attach` by cherry-picking `cb225bf00` onto a fresh branch off main (not a rebase; B.3 names what to leave out), the wait for Onyx's health and `SND-ONYX-016`, reverse its loopback refusal, `AW_SINDRI_ONYX_URL` and `AW_SINDRI_ONYX_FORWARD_URL`, the foreign-account refusal (`SND-ONYX-014`), the no-op when already attached, `attach --adopt`, the version-mismatch update, `onyx up`/`down` refused in pod mode (`SND-ONYX-013`), Onyx off (`SND-ONYX-015`, `AW_SINDRI_ONYX_ENABLED`, the doctor and preview texts), Temporal headless in pod mode (A2f), doctor's Onyx rows (containers answering, reachable, pin matches; it reads `AW_SINDRI_ONYX_PORTS`, which K-O1 writes) and the attach end-to-end proof (on the kind run), the catalog row and `Aw.Sindri.Ui.Tests` golden | nothing for the code: **no dependency on K-S0 or K-A1**, so any lane with room can take it (the doctor rows' port list comes from K-O1; until then its tests fake `AW_SINDRI_ONYX_PORTS`) |
| **K-C2 repo-pull** | engine | the service (it reads the approved profile only and clones again when a repo's directory is missing), `repo add --url --name`, its codes and doctor rows, the lag interface (it also reports each repo's update state: cloning, updating, cannot be updated) and metric, the `pull:` profile keys in `docs/csharp/sindri-profile.md` | A2d (its git calls take a credential, so they go through the allowlisted environment); the service and its tests start against the factory's interface before A2d merges |
| **K-C1 chart** | context | the Sindri chart with **Sindri and Ollama and `onyx.enabled: false`** (the containers and their first pull, no Service, the exec probes, the three claims (Sindri's, Onyx's and Ollama's, each mounted only where C.1 says), the `ephemeral-storage` limits and `sizeLimit`s of Sindri and Ollama, the mesh opt-outs, `values.schema.json`, the example values, the `helm lint`/`template` test, the pod-state table in the NOTES, `podAnnotations`, `podManagementPolicy: Parallel`, `sindri.port`, the `git.credentials` mapping, Sindri's container first and the volume init container on Sindri's image), and its kind test; K-O1 adds the Onyx containers to it. With K-A1, K-C2 and K-C3 it is **milestone 1** (§C.1) | K-A1; K-C2 and K-C3 for the full kind test (**not** K-S0 or K-O1) |
| **K-C3 connect** | context | `scripts/sindri-pod.sh connect` (waiting for the `sindri` container, not the pod's Ready; and `--onyx`, forwarding Onyx's port on the same local port, printing the address, running `onyx attach`) and `profile edit` (running `profile init` when no profile exists, the conflict message, the exit codes of §C.3) with `sindri profile validate --file`, the thin `repo add`, `approve` and `doctor` wrappers, `--release`/`--pod`/`--context`/`--namespace` on every verb, `connect --onyx --adopt`, its bash test with a fake `kubectl` (added to AGENTS.md's test list), including a stale-epoch case, an Onyx container looping with Sindri running, a taken Onyx port, `profile edit` cases, attach failures passing through with the app still connected, and the `NO_COLOR` and non-tty output | K-A1 for the heavy proof only; K-O2 for the real `attach` |
| **A2d child environment** | context | the allowlisted child environment factory and its architecture test | nothing (K-C2 needs it) |
| **A2c doctor rows** | context | the pod-mode doctor rows (the Ollama model's digest among them) except two groups: the Onyx rows are K-O2's and the repo rows are K-C2's | K-A1 |
| **K-I1 ideas** | ideas | the follow-ups that don't block the first deploy: the freshness dashboard on the Health page, a model provider for the pod (K12's later spec), where agents run and how they connect to Sindri (B.4; its own spec), a token or Unix socket for callers inside the pod (§6), TLS by mesh notes; each is written as a one-page spec, then taken as its own phase | nothing |
| **Part D W1–W5** | context, ui | as its own §9 (W5 is the `context_map_whole` tool: a projection plus a tool row, the context lane's, touching `Aw.Sindri.Host` as the context endpoints do) | W5 needs W1, W2 and the counts PR (`feat/context-whole-map-counts`) and nothing from the pod phases; the rest runs alongside everything |
| **K-E2E** | context | the kind run of §7 end to end, the docs (`docs/sindri/deploy.md`) | everything above |

Every phase's gate also runs `scripts/check-env-example.sh` and `node scripts/check-deps.mjs` (§8's variables go in
`.env.example`, and a new edge goes in `deps.yaml`). Every phase writes its own part of `docs/sindri/deploy.md` (and its API §9
rows, the chart's NOTES, and the AGENTS.md command lines it adds) in the same PR; K-E2E only joins them up.

Parallel: at the start, four lanes are busy at once: context on K-A1, A2d and Part D W1 (K-S0 as soon as K-A1's image builds, K-O1 as
soon as K-S0 reports), engine on K-C2, ui on Part D W3 (against W1's goldens), and api on K-O2 (it needs nothing else to start, B.3; its tests fake `AW_SINDRI_ONYX_PORTS`, and the real value arrives with K-O1's render), then
reviews. Context takes K-C3 and K-C1 (Sindri and Ollama, Onyx off) after K-A1, without waiting for K-S0, which outranks them in the
context lane; ui takes W4 after W3. K-E2E is last. The ideas lane starts with its one-page specs at once, and after each of api, engine
and ui finishes its phase it takes that lane's next item from K-I1, so no lane idles. Every phase is one PR with a scoped
gate (build plus the unit and integration lanes of the touched projects; `Aw.Sindri.Ui.Tests` in any phase that adds an API golden
or a catalog row).

## 10. Left to the builders

The chart's exact resource numbers (measured on the kind run), the start script's details, the nginx `sed` (K-S0 verifies it), each Onyx container's security context (K-S0), the pod-state table's wording, and the kind cluster's
version. Each is recorded in the code or `docs/sindri/deploy.md` where it lands.

## 11. History: revisions 3 to 5 (short)

*Revisions 3 to 5 changed these things; none of it is current except where the sections above say so.*

- **q73 (revision 3):** Onyx's prepackaged images run as sibling containers of the pod from one chart (§A.3), replacing the Docker-in-Docker sidecar. Gone with it: `privileged`, Sysbox, the Docker CLI and Compose plugin in the image, and the Docker-memory variables. `sindri onyx attach` came back, smaller (§B.3). Onyx's Secret, one `onyx-data` claim with subPaths and the render script with its drift check (K-O1) were added.
- **q74, q78, q79, q80 (revisions 3 and 4):** a cluster door for other pods (a `shared` caller, a Service, an any-address listener) was added in revision 4 and removed again in revision 5 when only agents in the same pod were to use Sindri. None of it is in this spec; the listeners are loopback only.
- **q82 (revision 5):** agents as containers of the pod, with a chart layout for five or more. All of it is cut in revision 6 (§13).
- **Review rounds 1 to 3 (`/autoplan`, 2026-10-23)** gave the parts that remain: no Onyx container gates Sindri and Onyx has liveness probes only; `podManagementPolicy: Parallel`; Onyx's Secret kept across uninstall; one sizing file shared by the managed and pod recipes; the render script's refusal of well-known fallback passwords; `attach` waiting for a starting Onyx and refusing a foreign first account; the numbered first-install steps; `repo-pull` reading the approved profile only; `ephemeral-storage` limits and `sizeLimit`s everywhere; the pod's listeners read once from `/proc/net/tcp`.

**Unverified here.** That `podManagementPolicy: Parallel` lets the StatefulSet controller replace an outdated pod that is not Ready (from the controller's documented behaviour, not run); that a Helm reinstall adopts the kept Secret; that the kubelet's ephemeral accounting behaves as C.1 Storage describes; that Onyx v4.9.0 can close sign-up after the first account (K-S0 item 6). The kind steps are the proof of each.

## 12. What revision 5 changed (short)

Revision 5 (q80, q82) removed the cluster door and put the agents in the pod; revision 6 removes the agents again, so only the removal of the cluster door stands: Sindri's API is a loopback-only listener, with no Service, no Host allowlist and no `share.*` profile keys, and the probes are `exec` probes because the kubelet cannot reach a loopback-only listener.

## 13. What revision 6 changed (the owner's answer q83, 2026-10-23)

The owner: "why are agents being pod contained that's over engineering. i just want sindri to be a docker image i can throw onto my aws infra". Cut:

- the `agents[]` chart values and containers, per-agent sizing, workspace claims, per-agent Secrets, the MCP config rendering and its flags, and the agent doctor rows (K4, K5 and phases K-AG and K-B1 are gone);
- the large-node tables and the agents-count formula: node sizing is Sindri + Onyx + Ollama, with one recommended node (§0);
- the agent restart, quiesce and ephemeral-storage arithmetic, and the Onyx sign-up closing as a requirement (it is a K-S0 check and a note now);
- `context.sessions.enabled` as a pod default, and the plain-words items about agents.

Kept: the image (Part A), Onyx's containers beside it, the volumes and backup, the owner's steps, `repo-pull`, Part D and K12. Added: the safety rule that nothing but Sindri's container mounts its data volume (B.2), so Ollama's weights moved to a claim of their own (`ollama-models`), and B.4 saying where agents run is out of scope.
