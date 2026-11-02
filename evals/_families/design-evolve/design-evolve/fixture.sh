#!/usr/bin/env bash
# Thin stub: the shared fixture is the dispatcher skill's.
exec bash "$(dirname "${BASH_SOURCE[0]}")/../../../design-evolve/fixture.sh" webtokens
