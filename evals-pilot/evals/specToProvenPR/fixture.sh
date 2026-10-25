#!/usr/bin/env bash
# Workspace for the specToProvenPR cases: a tiny greeter CLI plus an approved spec.
#   $1 = fresh   feature not built yet (spec -> PRs from scratch)
#   $1 = halfway --shout is implemented in core.py and unit-tested, but never wired to the CLI
set -eu
AW_LIB="$(cd "$(dirname "${BASH_SOURCE[0]}")/../_lib" && pwd)"
. "$AW_LIB/shim.sh"
shim_toolkit
variant="${1:-fresh}"
mkdir -p greeter tests planning/specs/greeter
: > greeter/__init__.py
: > tests/__init__.py
cat > planning/specs/greeter/DESIGN.md <<'P'
# Greeter: shout and language flags

Status: APPROVED (2026-10-20, owner)

The `greeter` CLI (`python3 -m greeter NAME`) prints `Hello, NAME!`.

## Phase 1: shout
`python3 -m greeter NAME --shout` prints the greeting in upper case: `HELLO, NAME!`.
Observable signal: running the command above in a terminal prints `HELLO, ADA!` for `ADA`.

## Phase 2: language
`python3 -m greeter NAME --lang es` prints `Hola, NAME!`. Unknown languages exit with status 2 and print
`unsupported language: XX` on stderr. `--lang` and `--shout` combine (`HOLA, ADA!`).
Observable signal: the three outputs above, plus the exit status of the unknown-language case.

Each phase ships as its own PR.
P
cat > README.md <<'P'
# greeter
Run: `python3 -m greeter NAME`. Tests: `python3 -m unittest`.
P
cat > tests/test_core.py <<'P'
import unittest

from greeter.core import greet


class Greet(unittest.TestCase):
    def test_plain(self):
        self.assertEqual(greet("Ada"), "Hello, Ada!")
P
if [ "$variant" = "halfway" ]; then
cat > greeter/core.py <<'P'
def greet(name, shout=False):
    message = f"Hello, {name}!"
    return message.upper() if shout else message
P
cat >> tests/test_core.py <<'P'

    def test_shout(self):
        self.assertEqual(greet("Ada", shout=True), "HELLO, ADA!")
P
cat > greeter/__main__.py <<'P'
import argparse

from greeter.core import greet


def main():
    parser = argparse.ArgumentParser(prog="greeter")
    parser.add_argument("name")
    parser.add_argument("--shout", action="store_true")
    args = parser.parse_args()
    print(greet(args.name))


main()
P
else
cat > greeter/core.py <<'P'
def greet(name):
    return f"Hello, {name}!"
P
cat > greeter/__main__.py <<'P'
import argparse

from greeter.core import greet


def main():
    parser = argparse.ArgumentParser(prog="greeter")
    parser.add_argument("name")
    args = parser.parse_args()
    print(greet(args.name))


main()
P
fi
git init -q -b main
git remote add origin git@github.com:acme/greeter.git
git add -A
git -c user.email=eval@example.com -c user.name=eval commit -q -m "greeter"
