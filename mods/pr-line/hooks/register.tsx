import { atom, read, update } from 'claude-code'
import type { EngineInterface, Register } from 'claude-code'

import type { Person, PrRow, Snapshot } from '../types'
import { QUERY, toRows } from './rows'
import type { ThreadMemory } from './rows'

const POLL_MS = 60_000
const EMPTY: Snapshot = { rows: [], updatedAt: null, error: null }
const snapshot = atom({ plugin: 'pr-line', key: 'snapshot' } as const, EMPTY, { shape: 'snapshot-2' })
// The git branch the session's directory is on; the line shows the open PR for it.
const sessionBranch = atom({ plugin: 'pr-line', key: 'branch' } as const, null)
// The GitHub repo (owner/name) of the session directory's origin remote. A PR matches only on repo AND branch,
// because the search spans every repo and the same branch name can be open in several.
const sessionRepo = atom({ plugin: 'pr-line', key: 'repo' } as const, null)
const MIN_GAP_MS = 15_000

const MARKS: Record<Person['mark'], string> = { approved: '✓', changes: '✗', requested: '?' }

function ciText(ci: PrRow['ci']): string {
  if (ci.state === 'pass') return 'CI ✓'
  if (ci.state === 'fail') return `CI ✗ ${ci.failing.join(', ')}`
  if (ci.state === 'running') return `CI ⋯ ${ci.done}/${ci.total}`
  return 'CI -'
}

function peopleText(people: Person[]): string {
  return `people: ${people.map(p => `${MARKS[p.mark]} ${p.login}${p.isOld ? ' (old)' : ''}`).join('   ')}`
}

let memory: ThreadMemory = {}
let lastPollAt = 0
const required: Record<string, string[]> = {}
let inFlight: Promise<void> | null = null

// owner/name from the https, scp-style ssh and ssh:// forms of a GitHub remote URL; null for any other host.
function repoOf(remote: string): string | null {
  // Drop the scheme and any user part, then expect github.com followed by ':' (scp style) or '/'.
  const hostAndPath = remote.trim().replace(/^[a-z+]+:\/\//i, '').replace(/^[^/]*@/, '')
  const m = /^github\.com[:/]([^/]+)\/([^/]+?)(?:\.git)?\/?$/.exec(hostAndPath)
  return m ? `${m[1]}/${m[2]}` : null
}

async function lookUpBranch($: EngineInterface): Promise<string | null> {
  const cwd = await $.session.cwd()
  const res = await $.process.run(['git', 'branch', '--show-current'], { cwd, timeoutMs: 5_000 })
  const name = res.exitCode === 0 ? res.stdout.trim() : ''
  let branch = name === '' || name === 'main' || name === 'master' ? null : name
  let repo: string | null = null
  if (branch !== null) {
    const remote = await $.process.run(['git', 'remote', 'get-url', 'origin'], { cwd, timeoutMs: 5_000 })
    repo = remote.exitCode === 0 ? repoOf(remote.stdout) : null
    // No GitHub origin: no PR can be this session's, so there is no line and no poll.
    if (repo === null) branch = null
  }
  if ((await read($, sessionBranch)) !== branch) await update($, sessionBranch, () => branch)
  if ((await read($, sessionRepo)) !== repo) await update($, sessionRepo, () => repo)
  return branch
}

// git missing or the directory gone: no branch, so no line and no poll, instead of a throw inside a timer or hook.
async function refreshBranch($: EngineInterface): Promise<string | null> {
  try {
    return await lookUpBranch($)
  } catch {
    if ((await read($, sessionBranch)) !== null) await update($, sessionBranch, () => null)
    return null
  }
}

// The PR of this session: same repo and same branch.
async function sessionRow($: EngineInterface, branch: string): Promise<PrRow | undefined> {
  const repo = await read($, sessionRepo)
  return (await read($, snapshot)).rows.find(r => r.repo === repo && r.branch === branch)
}

// The repo's required check names. Only a successful call is cached: a failed or timed-out one returns [] for this
// poll and is asked again on the next, so a rate-limited first call can't hide a required check for the session.
async function requiredChecks($: EngineInterface, repo: string): Promise<string[]> {
  if (required[repo]) return required[repo]
  try {
    const res = await $.process.run(
      [
        'gh',
        'api',
        `repos/${repo}/rules/branches/main`,
        '--jq',
        '[.[] | select(.type=="required_status_checks") | .parameters.required_status_checks[].context]',
      ],
      { timeoutMs: 10_000 },
    )
    if (res.exitCode !== 0) return []
    required[repo] = JSON.parse(res.stdout || '[]') as string[]
    return required[repo]
  } catch {
    return []
  }
}

// A second caller waits on the poll already running instead of starting another.
function poll($: EngineInterface): Promise<void> {
  inFlight ??= fetchRows($).finally(() => {
    inFlight = null
  })
  return inFlight
}

async function fetchRows($: EngineInterface): Promise<void> {
  lastPollAt = await $.clock.now()
  try {
    const res = await $.process.run(['gh', 'api', 'graphql', '-f', `query=${QUERY}`], { timeoutMs: 30_000 })
    if (res.exitCode !== 0) {
      const error = res.stderr.trim().split('\n')[0] || `gh exited ${res.exitCode}`
      await update($, snapshot, s => ({ ...s, error }))
      return
    }
    const json: unknown = JSON.parse(res.stdout)
    const repos = new Set(
      ((json as { data?: { search?: { nodes?: { repository?: { nameWithOwner: string } }[] } } }).data?.search
        ?.nodes ?? [])
        .map(n => n.repository?.nameWithOwner)
        .filter((r): r is string => typeof r === 'string'),
    )
    for (const repo of repos) await requiredChecks($, repo)
    const now = await $.clock.now()
    const built = toRows(json, required, memory, now)
    memory = built.memory
    await update($, snapshot, () => ({ rows: built.rows, updatedAt: now, error: null }))
  } catch (err) {
    const error = err instanceof Error ? err.message : String(err)
    await update($, snapshot, s => ({ ...s, error }))
  }
}

export const register: Register = on => {
  on('session.start', async ($, e, next) => {
    const started = await next(e)
    // Polls only while the session is on a branch other than main.
    $.clock.every(POLL_MS, async () => {
      if ((await refreshBranch($)) !== null) await poll($)
    })
    if ((await refreshBranch($)) !== null) void poll($)
    return started
  })

  // A turn can switch branch (a new worktree) or open the PR; look again without waiting for the timer.
  on('turn.complete', async ($, e, next) => {
    const result = await next(e)
    const before = await read($, sessionBranch)
    const branch = await refreshBranch($)
    if (branch === null) return result
    const hasRow = (await sessionRow($, branch)) !== undefined
    if (branch !== before || (!hasRow && (await $.clock.now()) - lastPollAt > MIN_GAP_MS)) void poll($)
    return result
  })

  // One line under the prompt. The PR number and title are left out: the hint line and status line show them.
  on('ui.render', { component: 'PromptHint' }, async ($, e, next) => {
    const hint = await next(e)
    const branch = await read($, sessionBranch)
    if (branch === null) return hint
    const row = await sessionRow($, branch)
    if (!row) return hint
    // gh failed on the last poll (missing, logged out, rate-limited): these rows are old, so say so.
    const isStale = (await read($, snapshot)).error !== null
    const { Box, Text } = $.ui.resolve(e)
    return (
      <Box flexDirection="column">
        {hint}
        <Text wrap="truncate-end">
          <Text color={row.ci.state === 'fail' ? 'error' : row.ci.state === 'pass' ? 'success' : 'subtle'}>
            {ciText(row.ci)}
          </Text>
          {row.bots !== null && `   bots ${row.bots.map(b => (b.isOnHead ? '✓' : '✗')).join('')}`}
          {`   threads ${row.threads}`}
          {row.isNewThread && <Text color="warning"> •</Text>}
          {row.people.length > 0 && `   ${peopleText(row.people)}`}
          <Text color={row.nextTone}>
            {'   -> '}
            {row.next}
          </Text>
          {isStale && <Text dimColor>{'   (stale)'}</Text>}
        </Text>
      </Box>
    )
  })
}
