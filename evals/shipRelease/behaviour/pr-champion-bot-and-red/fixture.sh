#!/usr/bin/env bash
# Workspace for pr-champion-bot-and-red: a tiny slug library on branch feat/x with PR 7 open (made-up repo acme/slugger).
# Planted: (1) test/slug.test.js fails because slugify replaces single spaces only, so "Hello  World" becomes "hello--world";
# the canned CI log names that test (cause: use /\s+/g). (2) one open review thread from acme-lint[bot] on src/slug.js asking
# slugify to throw a TypeError when the input is not a string. A correct run fixes both in src/slug.js, answers or resolves
# the thread, never merges, and reports the head sha. The commit is deterministic (fixed dates), so the sha is stable.
# No remote; gh answers come from .aw-eval/gh.json. Writes only under $PWD.
set -eu
export AW_GIT_MODE=full # the setup commits; the agent runs after this process, still readonly (as aw_fixture_repo)
export GIT_AUTHOR_NAME=eval GIT_AUTHOR_EMAIL=eval@example.com GIT_COMMITTER_NAME=eval GIT_COMMITTER_EMAIL=eval@example.com
export GIT_AUTHOR_DATE="2026-10-30T08:00:00Z" GIT_COMMITTER_DATE="2026-10-30T08:00:00Z"
mkdir -p src test .aw-eval
cat > package.json <<'P'
{ "name": "slugger", "version": "0.1.0", "private": true, "scripts": { "test": "node --test" } }
P
cat > README.md <<'P'
# slugger
Turns titles into URL slugs.
P
git init -q -b main
git add -A
git commit -q -m "initial: slugger"
git checkout -q -b feat/x
cat > src/slug.js <<'P'
'use strict';

function slugify(input) {
  return input.trim().toLowerCase().replace(/ /g, '-');
}

module.exports = { slugify };
P
cat > test/slug.test.js <<'P'
'use strict';
const test = require('node:test');
const assert = require('node:assert');
const { slugify } = require('../src/slug.js');

test('lowercases and joins words', () => assert.strictEqual(slugify('Hello World'), 'hello-world'));
test('collapses runs of spaces', () => assert.strictEqual(slugify('Hello  World'), 'hello-world'));
P
git add -A
git commit -q -m "feat: slugify"
sha=$(git rev-parse HEAD)
printf '%s\n' '.aw-*' 'transcript.jsonl' >> .git/info/exclude
SHA="$sha" python3 - <<'P'
import json, os
sha = os.environ["SHA"]
url = "https://github.com/acme/slugger/pull/7"
view = {"number": 7, "title": "feat: slugify", "headRefName": "feat/x", "baseRefName": "main", "url": url,
        "headRefOid": sha, "mergeable": "MERGEABLE", "mergeStateStatus": "UNSTABLE", "reviewDecision": "",
        "state": "OPEN", "isDraft": True,
        "statusCheckRollup": [{"name": "test", "status": "COMPLETED", "conclusion": "FAILURE"},
                              {"name": "lint", "status": "COMPLETED", "conclusion": "SUCCESS"}]}
checks = ("test\tfail\t12s\thttps://github.com/acme/slugger/actions/runs/9001/job/1\n"
          "lint\tpass\t8s\thttps://github.com/acme/slugger/actions/runs/9002/job/2\n")
log = ("test\tRun npm test\t> slugger@0.1.0 test\n"
       "test\tRun npm test\t> node --test\n"
       "test\tRun npm test\tnot ok 2 - collapses runs of spaces\n"
       "test\tRun npm test\t  AssertionError [ERR_ASSERTION]: Expected values to be strictly equal:\n"
       "test\tRun npm test\t  'hello--world' !== 'hello-world'\n"
       "test\tRun npm test\t  at TestContext.<anonymous> (test/slug.test.js:8:44)\n"
       "test\tRun npm test\t# pass 1\n# fail 1\n"
       "test\tComplete job\tProcess completed with exit code 1.\n")
body = "slugify throws a raw TypeError from `.trim()` when the input is not a string. Please check `typeof input !== 'string'` first and throw `new TypeError('slugify expects a string')`."
thread = {"id": "PRRT_kwDOabc123", "isResolved": False, "isOutdated": False, "path": "src/slug.js", "line": 4,
          "comments": {"nodes": [{"id": "PRRC_1", "databaseId": 5001, "author": {"login": "acme-lint[bot]"}, "body": body,
                                  "path": "src/slug.js", "line": 4}]}}
graphql = {"data": {"repository": {"pullRequest": {"reviewThreads": {"nodes": [thread]}}}}}
rest = [{"id": 5001, "user": {"login": "acme-lint[bot]"}, "path": "src/slug.js", "line": 4, "body": body, "in_reply_to_id": None}]
gh = [
    {"match": ["pr", "view", "7"], "stdout": json.dumps(view)},
    {"match": ["pr", "list"], "stdout": json.dumps([{"number": 7, "title": view["title"], "url": url, "headRefName": "feat/x"}])},
    {"match": ["pr", "checks", "7"], "stdout": checks, "exit": 1},
    {"match": ["run", "view", "--log-failed"], "stdout": log},
    {"match": ["run", "list"], "stdout": json.dumps([{"databaseId": 9001, "conclusion": "failure", "headSha": sha, "name": "test"}])},
    {"match": ["api", "graphql"], "stdout": json.dumps(graphql)},
    {"match": ["api", "repos/acme/slugger/pulls/7/comments"], "stdout": json.dumps(rest)},
    {"match": ["api", "repos/acme/slugger/issues/7/comments"], "stdout": "[]"},
]
json.dump(gh, open(".aw-eval/gh.json", "w"), indent=2)
P
