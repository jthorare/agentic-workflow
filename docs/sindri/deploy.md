# Deploying Sindri as an image

The spec is `docs/superpowers/specs/2026-10-23-sindri-deployable-context-design.md` (Part A). This page covers the image only
(phase K-A1); the charts, the shared door and the owner's steps land with their own phases and add their sections here.

## What is in the image

The `Dockerfile` at the repo root has three stages, every `FROM` pinned by digest from `deploy/images.lock`:

| Stage | Does |
|---|---|
| build | Publishes the Sindri host the way `scripts/sindri-app.sh` does (framework-dependent, the shipped recipes embedded through `AwRecipesDir`), then `judge` and `aw-packets` NativeAOT for the target platform, then runs `sindri recipe eval --shipped` and fails the build if an eval fails or the embedded recipe count differs from `recipes/sindri/*.yaml`. `sindri-ui` is not built: the image has no desktop |
| tools | The `temporal` CLI from `binaries.lock` (downloaded and sha256-checked by `scripts/binaries-lock.sh fetch temporal`), and graphify in a hash-pinned venv at `context/code-index.lock`'s pin (`uv`, the same install `scripts/install-deps.sh` does) |
| runtime | `aspnet` plus `git`, `bubblewrap` and `tini`; uid 10001; everything Sindri ships under `/opt/sindri` |

`ENTRYPOINT ["tini", "--", "/opt/sindri/start.sh"]`. `start.sh` (source: `deploy/start.sh`) copies the baked-in Temporal binary to
`$AW_STATE_DIR/cache/bin/temporal-<version>/` when the volume does not hold it (Sindri looks for it only there), then `exec`s
`sindri serve`. `serve` stops cleanly on SIGTERM and stops its Temporal dev server within 10 s; give the pod a 60 s grace period.

## Environment

All set by the image; override with `docker run -e` or the chart's values.

| Variable | Image value | Meaning |
|---|---|---|
| `AW_SINDRI_DEPLOY` | `pod` | The pod-mode switch. Unset (any desktop install) is today's behaviour; `sindri doctor` echoes `deploy:mode` in pod mode. Nothing else keys on it yet |
| `AW_SINDRI_ONYX_HOSTS` | (the chart sets it) | The Onyx host names this pod may reach at a cluster address. Only read in pod mode; see "Onyx by its cluster address" |
| `AW_STATE_DIR` | `/data/state` | State root; Temporal's binary is seeded under `cache/bin/` |
| `AW_SINDRI_HOME` | `/data/state/sindri-app` | Ledger, views, approvals, endpoint record, token |
| `HOME`, `XDG_CACHE_HOME`, `UV_CACHE_DIR` | under `/data/home` | The git, uv and graphify caches; a read-only root with uid 10001 has no other writable home |
| `AW_SINDRI_JUDGE`, `AW_PACKETS_BIN` | `/opt/sindri/judge`, `/opt/sindri/aw-packets` | The NativeAOT tools the recipes run |
| `AW_CODEINDEX_GRAPHIFY`, `AW_CODE_INDEX_LOCK` | `/opt/sindri/graphify/bin/graphify`, `/opt/sindri/context/code-index.lock` | The code index's graphify and its pin file |

## Volume layout

One volume at `/data` (writable by uid 10001), nothing else but `/tmp` is writable (run with `--read-only --tmpfs /tmp`):

```
/data/state/sindri-app/   ledger.db, views.db, approved/, auth/, control.json, logs   (back this up; views and the code index rebuild)
/data/state/cache/bin/temporal-<ver>/temporal                                          (seeded by start.sh)
/data/home/               git, uv and graphify caches
```

Sindri's databases allow one writer, so run one container per volume. The lease records the container's host name; a restarted
container with the same name takes the lease at once when the old holder is provably dead (`PodRestartLeaseTests`). Never
force-delete a StatefulSet pod `sindri-0` to restart it: a force delete can leave the old process holding the volume.

## Build and run

```bash
docker build -t sindri .                                  # add --platform linux/arm64 for arm; --build-arg SOURCE_COMMIT=$(git rev-parse HEAD)
docker volume create sindri-data
docker run -d --name sindri --read-only --tmpfs /tmp --cap-drop ALL --security-opt no-new-privileges -v sindri-data:/data sindri
docker exec sindri /opt/sindri/sindri doctor
```

The build needs network (apt, the Temporal release, PyPI for graphify) and a NativeAOT-capable builder (the image installs
`clang` and `zlib1g-dev` in the build stage). Without user namespaces `bwrap` cannot start, and the code graph stays off
(`SND-CODEIDX-007`); the text and symbol index still work.

The heavy proof is `deploy/test/image-smoke.sh` (build, fresh volume, serve up, doctor, a restart and a killed restart each take
the lease at once). It needs Docker access and is not part of any PR gate's unit lanes.

## Pins

`deploy/images.lock` holds every third-party image by index digest and per-platform digests; `scripts/images-lock.sh check`
checks its shape and that the `Dockerfile` names only pinned images (`bash scripts/tests/images-lock.test.sh`). Onyx's chart
images are `todo` rows until the charts phase (K-C1) resolves them. To bump a base image: look the new digests up with
`docker buildx imagetools inspect <repository>:<tag>`, change the lock rows and the `Dockerfile` `FROM`, run the check.

## Onyx by its cluster address

In a pod Sindri does not start Onyx; it connects to the Onyx release that runs beside it. Two things decide whether it may:

- The environment: `AW_SINDRI_DEPLOY=pod` and the Onyx Service's host name in `AW_SINDRI_ONYX_HOSTS`. A profile cannot widen this. Outside pod mode, or for a host not in the list, a cluster address is refused (`SND-CONTEXT-055`) and `context_query` answers with its code and docs hits and a note that Onyx is unavailable.
- The profile: `context.sources.onyx` with `network: cluster`, `managed: false` and the Service URL (`http` is allowed; the traffic stays inside the namespace).

`sindri onyx attach <url>` sets this up: the URL must answer `/api/health` and report the release `context/onyx.lock` pins; then the same account steps as `sindri onyx up` run against it (your sign-up, Sindri's dedicated user and token, the profile section). It is the human's verb: run it at a terminal in the pod (`sindri-pod.sh connect --onyx` does). The Onyx on the same machine is `sindri onyx up`'s, and `attach` refuses a loopback address.
