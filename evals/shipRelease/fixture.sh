#!/usr/bin/env bash
# Workspace for the shipRelease cases: a small recipe-scaling library on a feature branch, tests green.
# Planted facts a correct run must report: the suite has 7 tests and all pass (`npm test`, node:test), there is
# no coverage tooling, and the diff touches only src/ and test/ (no UI, so app verification is n/a).
# No remote (evals/_lib/fixture-lib.sh: a stray push has nowhere to go), so the sync step has no origin to
# fetch and says so. `gh pr list` has one canned answer (no PR yet); gh writes are refused under readonly.
# Writes only under $PWD.
set -eu
mkdir -p src test .aw-eval
cat > package.json <<'P'
{ "name": "pantry-scale", "version": "0.3.0", "private": true, "scripts": { "test": "node --test" } }
P
cat > README.md <<'P'
# pantry-scale
Scales recipe ingredient quantities to a new serving count.
P
cat > src/scale.js <<'P'
'use strict';

function scaleQuantity(quantity, fromServings, toServings) {
  if (fromServings <= 0 || toServings <= 0) throw new RangeError('servings must be positive');
  return (quantity * toServings) / fromServings;
}

module.exports = { scaleQuantity };
P
cat > test/scale.test.js <<'P'
'use strict';
const test = require('node:test');
const assert = require('node:assert');
const { scaleQuantity } = require('../src/scale.js');

test('doubles a quantity', () => assert.strictEqual(scaleQuantity(2, 4, 8), 4));
test('keeps a quantity at the same servings', () => assert.strictEqual(scaleQuantity(3, 4, 4), 3));
test('scales down to one serving', () => assert.strictEqual(scaleQuantity(8, 8, 1), 1));
P
git init -q -b main
git add -A
git -c user.name=eval -c user.email=eval@example.com commit -q -m "initial: scaleQuantity"
git checkout -q -b feat/halve-servings
cat > src/scale.js <<'P'
'use strict';

function scaleQuantity(quantity, fromServings, toServings) {
  if (fromServings <= 0 || toServings <= 0) throw new RangeError('servings must be positive');
  return (quantity * toServings) / fromServings;
}

function halve(quantity) {
  return scaleQuantity(quantity, 2, 1);
}

function roundToEighth(quantity) {
  return Math.round(quantity * 8) / 8;
}

module.exports = { scaleQuantity, halve, roundToEighth };
P
cat > test/halve.test.js <<'P'
'use strict';
const test = require('node:test');
const assert = require('node:assert');
const { halve, roundToEighth, scaleQuantity } = require('../src/scale.js');

test('halves a whole quantity', () => assert.strictEqual(halve(4), 2));
test('halves a fraction', () => assert.strictEqual(halve(0.5), 0.25));
test('rounds to the nearest eighth', () => assert.strictEqual(roundToEighth(0.3), 0.25));
test('rejects zero servings', () => assert.throws(() => scaleQuantity(1, 0, 2), RangeError));
P
git add -A
git -c user.name=eval -c user.email=eval@example.com commit -q -m "feat: halve and roundToEighth helpers"
# The run's own scratch must not make the tree dirty: shipRelease stops on a dirty tree.
printf '%s\n' '.aw-*' 'transcript.jsonl' >> .git/info/exclude
cat > .aw-eval/gh.json <<'J'
[
  {"match": ["pr", "list"], "stdout": "[]", "stderr": "", "exit": 0}
]
J
