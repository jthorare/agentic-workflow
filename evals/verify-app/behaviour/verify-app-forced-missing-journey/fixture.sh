#!/usr/bin/env bash
# Thin stub: scaffold_script must sit inside the case dir; the shared fixture lives two levels up. No plans/add-book.md on purpose.
exec bash "$(dirname "${BASH_SOURCE[0]}")/../../fixture.sh" web
