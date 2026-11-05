# .devcontainer/Dockerfile — the human development environment (readiness spec §4d, §6 W5).
# For people only: it is never the agent sandbox, and it has no Docker-in-Docker and no host Docker socket
# (Docker-in-Docker means root on the host; agent sessions use the Sindri containers, main spec §8.2).
#
# Pins: the base image by digest (its tag is the exact SDK in global.json), Node by version and SHA-256 (the
# major in .nvmrc), uv by image digest. scripts/tests/repo-templates.test.sh checks they stay in step.

# .NET SDK 10.0.403 (global.json: rollForward disable) on Ubuntu 24.04.
FROM mcr.microsoft.com/dotnet/sdk:10.0.403-noble@sha256:de4fe90a98845d75910b9769662465c84de1a934665c431279d1382b2bddc02b

# uv 0.12.17 (published 2026-09-18).
COPY --from=ghcr.io/astral-sh/uv:0.12.17@sha256:10787c682e4184e4f290de1171fd4703dc63de99221f10fe1c99002ce7fa9acc /uv /uvx /usr/local/bin/

# Tools the repo's scripts and checks use: git, jq, sqlite3 (state inspection), shellcheck and shfmt (spec §4:
# bash in config/, scripts/, providers/), ruby (its stdlib YAML parser, for scripts/tests/repo-templates.test.sh),
# xz-utils (the Node tarball), python3 and build-essential (native modules such as better-sqlite3).
RUN apt-get update \
 && apt-get install -y --no-install-recommends \
      ca-certificates curl git jq python3 build-essential ruby shellcheck shfmt sqlite3 xz-utils \
 && apt-get clean \
 && find /var/lib/apt/lists -type f -delete

# Node 22.23.3 (published 2026-09-23; .nvmrc says 22), verified against the release's SHASUMS256.txt.
ARG NODE_VERSION=22.23.3
ARG NODE_SHA256_X64=df450af89261115ef9f9e3830c3eeb2cc9213b63c720b1af623cb5dcbe2e02de
ARG NODE_SHA256_ARM64=a44aeb94849a299b22df10b9e622ec2f605c2183501bc40590705131de7c740f
ARG TARGETARCH
RUN set -eu; \
    case "${TARGETARCH:-amd64}" in \
      amd64) arch=x64;   sum="$NODE_SHA256_X64" ;; \
      arm64) arch=arm64; sum="$NODE_SHA256_ARM64" ;; \
      *) echo "unsupported architecture: ${TARGETARCH}" >&2; exit 1 ;; \
    esac; \
    tarball="node-v${NODE_VERSION}-linux-${arch}.tar.xz"; \
    curl -fsSLo "/tmp/$tarball" "https://nodejs.org/dist/v${NODE_VERSION}/$tarball"; \
    echo "$sum  /tmp/$tarball" | sha256sum -c -; \
    tar -xJf "/tmp/$tarball" -C /usr/local --strip-components=1 --no-same-owner; \
    unlink "/tmp/$tarball"; \
    node --version; npm --version

ENV DOTNET_CLI_TELEMETRY_OPTOUT=1 \
    DOTNET_NOLOGO=1 \
    AVALONIA_TELEMETRY_OPTOUT=1

# The noble base ships the non-root user "ubuntu" (uid 1000); the container runs as that user.
USER ubuntu
