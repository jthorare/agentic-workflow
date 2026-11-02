#!/usr/bin/env bash
# scaffold_script must sit inside the case dir; the skill's shared fixture lives two levels up (staged layout too).
exec bash "$(dirname "${BASH_SOURCE[0]}")/../../fixture.sh" fresh
