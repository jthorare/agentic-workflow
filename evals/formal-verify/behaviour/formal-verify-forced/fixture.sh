#!/usr/bin/env bash
# Workspace for the formal-verify forced case: a made-up tide-report service whose nightly job claim reads the owner, then
# writes it in a second statement (check-then-set), and a doc that claims only one worker ever runs a job. No remote, writes
# only under $PWD.
set -eu
export AW_GIT_MODE=full # the setup commits; the agent runs after this process, still readonly (as aw_fixture_repo)
git init -q -b main
mkdir -p src/jobs docs
cat > AGENTS.md <<'MD'
# Tide reports

Builds the nightly tide report for each harbour. Workers claim report jobs from the `job` table.

- Code: `src/jobs/` (claiming and running jobs).
- Docs: `docs/`.
MD
cat > src/jobs/claim.py <<'PY'
from db import db


def claim(job_id, worker):
    row = db.one("select owner from job where id = %s", [job_id])
    if row.owner is None:
        db.run("update job set owner = %s where id = %s", [worker, job_id])
        return True
    return False


def release(job_id, worker):
    db.run("update job set owner = null where id = %s and owner = %s", [job_id, worker])
PY
cat > docs/claim.md <<'MD'
# Job claim

Any number of workers poll for report jobs. A worker calls `claim(job_id, me)` and runs the job only when it returns True.
Each statement runs in its own autocommit transaction.

Guarantee: only one worker ever runs a given report job at a time.
MD
git add -A
git -c user.email=fixture@example.invalid -c user.name=fixture commit -q -m "tide reports"
