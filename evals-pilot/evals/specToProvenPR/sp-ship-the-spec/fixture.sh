#!/usr/bin/env bash
# Thin stub: scaffold_script must sit inside the case dir; the shared fixture lives one level up.
exec bash "$(dirname "${BASH_SOURCE[0]}")/../fixture.sh" fresh
