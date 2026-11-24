import { atom, read, update } from 'claude-code'
import type { ElementTable, EngineInterface, Register } from 'claude-code'

import type { Person, PrRow, Snapshot } from '../types'
import { BOTS, QUERY, toRows } from './rows'
import type { ThreadMemory } from './rows'

const HOME_REPO = Object.keys(BOTS)[0]
const PANE = 'my-prs'
const POLL_MS = 60_000
const EMPTY: Snapshot = { rows: [], updatedAt: null, error: null }
const snapshot = atom({ plugin: 'my-prs', key: 'snapshot' } as const, EMPTY, { shape: 'snapshot-2' })
// The git branch the session's directory is on; the band shows the open PR for it.
const sessionBranch = atom({ plugin: 'my-prs', key: 'branch' } as const, null)
const BAND_MIN_GAP_MS = 15_000

const MARKS: Record<Person['mark'], string> = { approved: '✓', changes: '✗', requested: '?' }

function clock(ms: number): string {
  const d = new Date(ms)
  return `${String(d.getHours()).padStart(2, '0')}:${String(d.getMinutes()).padStart(2, '0')}`
}

function ciText(ci: PrRow['ci']): string {
  if (ci.state === 'pass') return 'CI ✓'
  if (ci.state === 'fail') return `CI ✗ ${ci.failing.join(', ')}`
  if (ci.state === 'running') return `CI ⋯ ${ci.done}/${ci.total}`
  return 'CI -'
}

function peopleText(people: Person[]): string {
  if (people.length === 0) return 'people: none requested'
  return `people: ${people.map(p => `${MARKS[p.mark]} ${p.login}${p.isOld ? ' (old)' : ''}`).join('   ')}`
}

// The CI, bots, threads, people and next-step lines the pane and the band both draw.
function rowLines(ui: Pick<ElementTable, 'Text'>, row: PrRow, indent: string) {
  const { Text } = ui
  return [
    <Text wrap="truncate-end">
      {indent}
      <Text color={row.ci.state === 'fail' ? 'error' : row.ci.state === 'pass' ? 'success' : 'subtle'}>
        {ciText(row.ci)}
      </Text>
      {row.bots !== null && `   bots ${row.bots.map(b => (b.isOnHead ? '✓' : '✗')).join('')}`}
      {`   threads ${row.threads}`}
      {row.isNewThread && <Text color="warning"> •</Text>}
    </Text>,
    <Text wrap="truncate-end" dimColor={row.people.length === 0}>
      {indent}
      {peopleText(row.people)}
    </Text>,
    <Text wrap="truncate-end" color={row.nextTone}>
      {indent}
      {'-> '}
      {row.next}
    </Text>,
  ]
}

let memory: ThreadMemory = {}
let lastPollAt = 0
const required: Record<string, string[]> = {}
let inFlight: Promise<void> | null = null
let isOpen = false

async function refreshBranch($: EngineInterface): Promise<string | null> {
  const cwd = await $.session.cwd()
  const res = await $.process.run(['git', 'branch', '--show-current'], { cwd, timeoutMs: 5_000 })
  const name = res.exitCode === 0 ? res.stdout.trim() : ''
  const branch = name === '' || name === 'main' || name === 'master' ? null : name
  if ((await read($, sessionBranch)) !== branch) await update($, sessionBranch, () => branch)
  return branch
}

async function requiredChecks($: EngineInterface, repo: string): Promise<string[]> {
  if (required[repo]) return required[repo]
  const res = await $.process.run([
    'gh',
    'api',
    `repos/${repo}/rules/branches/main`,
    '--jq',
    '[.[] | select(.type=="required_status_checks") | .parameters.required_status_checks[].context]',
  ])
  required[repo] = res.exitCode === 0 ? (JSON.parse(res.stdout || '[]') as string[]) : []
  return required[repo]
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
    await $.command.register({ name: 'prs', description: 'Open the My PRs pane' })
    // Polls while the pane is open or the session is on a branch other than main.
    $.clock.every(POLL_MS, async () => {
      const branch = await refreshBranch($)
      if (isOpen || branch !== null) await poll($)
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
    const snap = await read($, snapshot)
    const hasRow = snap.rows.some(r => r.branch === branch)
    if (branch !== before || (!hasRow && (await $.clock.now()) - lastPollAt > BAND_MIN_GAP_MS)) void poll($)
    return result
  })

  // One line under the prompt. The PR number and title are left out: the hint line and status line show them.
  on('ui.render', { component: 'PromptHint' }, async ($, e, next) => {
    const hint = await next(e)
    const branch = await read($, sessionBranch)
    if (branch === null) return hint
    const row = (await read($, snapshot)).rows.find(r => r.branch === branch)
    if (!row) return hint
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
        </Text>
      </Box>
    )
  })

  on('command.run', { command: 'prs' }, async $ => {
    await $.ui.open({ id: PANE, title: 'My PRs' })
    isOpen = true
    await poll($)
    return { text: 'My PRs pane opened.' }
  })

  on('ui.close', { id: PANE }, async ($, e, next) => {
    isOpen = false
    return next(e)
  })

  on('ui.render', { component: 'Pane', requestId: PANE }, async ($, e) => {
    const { Box, Text, Link } = $.ui.resolve(e)
    const snap = await read($, snapshot)

    return (
      <Box flexDirection="column">
        <Text dimColor>
          {snap.updatedAt === null ? 'loading...' : `updated ${clock(snap.updatedAt)}`}
        </Text>
        {snap.error !== null && (
          <Text color="error" wrap="truncate-end">
            gh: {snap.error}
          </Text>
        )}
        {snap.updatedAt !== null && snap.rows.length === 0 && <Text dimColor>No open PRs.</Text>}
        {snap.rows.map(row => (
          <Box flexDirection="column" marginTop={1}>
            <Text wrap="wrap">
              <Link href={row.url}>#{row.number}</Link> {row.title}
              {row.repo === HOME_REPO ? '' : ` (${row.repo})`}
            </Text>
            {rowLines({ Text }, row, '  ')}
          </Box>
        ))}
      </Box>
    )
  })
}
