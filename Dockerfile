# syntax=docker/dockerfile:1
# The Sindri image (spec docs/superpowers/specs/2026-10-23-sindri-deployable-context-design.md §A.1; docs/sindri/deploy.md).
# Build from the repo root:  docker build -t sindri .   (add --platform linux/arm64 for arm)
#
# Every FROM is a digest from deploy/images.lock (scripts/images-lock.sh check fails when one drifts). Three stages:
#   build    publishes the Sindri host exactly as scripts/install-cs.sh does (framework-dependent, the shipped recipes embedded),
#            then judge and aw-packets NativeAOT for the target platform, then runs the shipped recipes' evals
#   tools    the temporal CLI from binaries.lock (sha256-checked) and graphify at the code index's pin (context/code-index.lock)
#   runtime  aspnet + git, bubblewrap, tini; uid 1000; read-only-root friendly (all state under /data)
# sindri-ui is not built: the image has no desktop.

FROM mcr.microsoft.com/dotnet/sdk:10.0.401-noble@sha256:e70cdb7f80b0348f5cb85f19a8f670fca061f033d57eed12fa003d58b0e06317 AS build
ARG TARGETARCH
# Commit and recipes tree hash, for doctor's build row (optional: the image has no git checkout to compare with)
ARG SOURCE_COMMIT=unknown
ARG RECIPES_TREE=
ENV DOTNET_CLI_TELEMETRY_OPTOUT=1 DOTNET_NOLOGO=1
# NativeAOT needs a C toolchain and zlib; jq reads the evals' result
RUN apt-get update \
 && apt-get install -y --no-install-recommends clang zlib1g-dev jq \
 && rm -rf /var/lib/apt/lists/*
WORKDIR /src
COPY . .
# The RID: the same one scripts/install-cs.sh derives from uname
RUN case "${TARGETARCH:-$(dpkg --print-architecture)}" in \
      amd64) echo linux-x64 > /rid ;; arm64) echo linux-arm64 > /rid ;; *) echo "unsupported arch ${TARGETARCH}" >&2; exit 1 ;; esac
# The host: framework-dependent, locked restore, the shipped recipes embedded (AwRecipesDir)
RUN dotnet publish src/Sindri/Aw.Sindri.Host/Aw.Sindri.Host.csproj -c Release --no-self-contained \
      -p:AwRecipesDir=/src/recipes/sindri -o /out/sindri
# judge and aw-packets, NativeAOT; the RID's packs are not in the lock files, so the restore is unlocked, as in install-cs.sh
RUN dotnet publish src/Judge/Aw.Judge.Host/Aw.Judge.Host.csproj -c Release -r "$(cat /rid)" -p:RestoreLockedMode=false -o /out/judge \
 && dotnet publish src/Packets/Aw.Packets.App/Aw.Packets.App.csproj -c Release -r "$(cat /rid)" -p:RestoreLockedMode=false -o /out/packets \
 && cp /out/judge/judge /out/sindri/judge && cp /out/packets/aw-packets /out/sindri/aw-packets \
 && cp /out/judge/libe_sqlite3.* /out/packets/libe_sqlite3.* /out/sindri/ \
 && printf 'commit=%s\nrecipes-tree=%s\n' "${SOURCE_COMMIT}" "${RECIPES_TREE}" > /out/sindri/build-info
# The shipped recipes' evals fail the build, as they fail the install-cs.sh publish; the embedded count must equal the recipes shipped
RUN /out/sindri/sindri recipe eval --shipped --json > /tmp/evals.json \
 && test "$(jq -r '.recipes | length' /tmp/evals.json)" = "$(find /src/recipes/sindri -maxdepth 1 -name '*.yaml' | wc -l)"

FROM mcr.microsoft.com/dotnet/sdk:10.0.401-noble@sha256:e70cdb7f80b0348f5cb85f19a8f670fca061f033d57eed12fa003d58b0e06317 AS tools
ARG TARGETARCH
RUN apt-get update \
 && apt-get install -y --no-install-recommends curl ca-certificates \
 && rm -rf /var/lib/apt/lists/*
COPY --from=ghcr.io/astral-sh/uv:0.13.1@sha256:4a1f3666816dafadd20ceb71eac9a7d56d7ef30279f7d64048519ab901ada336 /uv /usr/local/bin/uv
WORKDIR /src
COPY binaries.lock ./
COPY scripts/binaries-lock.sh scripts/binaries-lock.sh
COPY context context
# temporal: downloaded and sha256-checked by the same script setup uses, into a seed tree that start.sh copies onto a new volume
RUN AW_STATE_DIR=/out/seed bash scripts/binaries-lock.sh fetch temporal
# graphify: a hash-pinned venv built where it will live (a venv names its own path), as scripts/install-deps.sh builds the
# code index's. uv's managed Python goes beside it so the runtime user can read both.
ENV UV_PYTHON_INSTALL_DIR=/opt/sindri/python UV_CACHE_DIR=/tmp/uv-cache
RUN set -eu; \
    ver="$(awk '$1=="graphify"{print $2}' context/code-index.lock)"; \
    date="$(awk '$1=="graphify"{print $3}' context/code-index.lock)"; \
    uv venv --quiet /opt/sindri/graphify --python 3.12 \
 && uv pip install --quiet --python /opt/sindri/graphify/bin/python --require-hashes -r context/code-index-requirements.txt --exclude-newer "$date" \
 && test "$(/opt/sindri/graphify/bin/graphify --version | grep -oE '[0-9]+\.[0-9]+\.[0-9]+' | head -1)" = "$ver"

FROM mcr.microsoft.com/dotnet/aspnet:10.0-noble@sha256:8af87eba5e7cdafb1514d7388be287365da97c132509e84dfe252ffd552398ca AS runtime
# git (repos), bubblewrap (the code graph's sandbox), tini (PID 1: it reaps the detached Temporal process), procps (/bin/ps
# and /bin/kill: the Temporal dev server's lifecycle finds and signals its process through them, DevServerProcesses), curl (the chart's exec probes, spec §A.1). The base
# image's `ubuntu` user holds uid 1000, so it is removed and `sindri` takes the uid (no Docker in the image).
# Onyx's sibling containers in the pod are pinned in context/onyx.lock, not deploy/images.lock.
RUN apt-get update \
 && apt-get install -y --no-install-recommends git bubblewrap tini procps curl ca-certificates \
 && rm -rf /var/lib/apt/lists/* \
 && { userdel --remove ubuntu 2>/dev/null || true; } \
 && useradd --uid 1000 --user-group --no-create-home --home-dir /data/home --shell /usr/sbin/nologin sindri \
 && mkdir -p /opt/sindri /data \
 && chown 1000:1000 /data
COPY --from=build /out/sindri /opt/sindri
COPY --from=tools /opt/sindri/graphify /opt/sindri/graphify
COPY --from=tools /opt/sindri/python /opt/sindri/python
COPY --from=tools /out/seed /opt/sindri/seed
COPY context/code-index.lock /opt/sindri/context/code-index.lock
COPY deploy/start.sh /opt/sindri/start.sh
# /opt/sindri is world-readable, so any uid the pod runs the image as can read what Sindri ships.
RUN chmod -R a+rX /opt/sindri \
 && chmod 0755 /opt/sindri/start.sh /opt/sindri/sindri /opt/sindri/judge /opt/sindri/aw-packets

# State is under /data (the volume); the root is read-only, /tmp is the only other writable path. Every variable a `kubectl exec`
# needs is set here, not in start.sh (an exec doesn't run it); the chart adds the rest (spec §A.1).
ENV AW_SINDRI_DEPLOY=pod \
    AW_STATE_DIR=/data/state \
    AW_SINDRI_HOME=/data/state/sindri-app \
    HOME=/data/home \
    XDG_CACHE_HOME=/data/home/.cache \
    UV_CACHE_DIR=/data/home/.cache/uv \
    TMPDIR=/tmp \
    AW_SINDRI_JUDGE=/opt/sindri/judge \
    AW_PACKETS_BIN=/opt/sindri/aw-packets \
    AW_CODEINDEX_GRAPHIFY=/opt/sindri/graphify/bin/graphify \
    AW_CODE_INDEX_LOCK=/opt/sindri/context/code-index.lock \
    DOTNET_EnableDiagnostics=0 \
    DOTNET_CLI_TELEMETRY_OPTOUT=1
USER 1000:1000
WORKDIR /data
VOLUME ["/data"]
ENTRYPOINT ["tini", "--", "/opt/sindri/start.sh"]
