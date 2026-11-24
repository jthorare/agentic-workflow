import { atom, read, update } from 'claude-code'
import type { ElementTable, EngineInterface, Register } from 'claude-code'

import type { Person, PrRow, Snapshot } from '../types'
import { QUERY, readConfig, toRows } from './rows'
import type { ModConfig, ThreadMemory } from './rows'

const PANE = 'my-prs'
const POLL_MS = 60_000
const EMPTY: Snapshot = { rows: [], updatedAt: null, error: null }
const snapshot = atom({ plugin: 'my-prs', key: 'snapshot' } as const, EMPTY, { shape: 'snapshot-2' })

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
const required: Record<string, string[]> = {}
let inFlight: Promise<void> | null = null
let isOpen = false
let timer: ReturnType<EngineInterface['clock']['every']> | null = null

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
function poll($: EngineInterface, cfg: ModConfig): Promise<void> {
  inFlight ??= fetchRows($, cfg).finally(() => {
    inFlight = null
  })
  return inFlight
}

async function fetchRows($: EngineInterface, cfg: ModConfig): Promise<void> {
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
    const built = toRows(json, required, memory, now, cfg)
    memory = built.memory
    await update($, snapshot, () => ({ rows: built.rows, updatedAt: now, error: null }))
  } catch (err) {
    const error = err instanceof Error ? err.message : String(err)
    await update($, snapshot, s => ({ ...s, error }))
  }
}

export const register: Register = (on, options) => {
  const cfg = readConfig(options)
  on('session.start', async ($, e, next) => {
    const started = await next(e)
    await $.command.register({ name: 'prs', description: 'Open the My PRs pane' })
    // Polls only while the pane is open.
    // One timer per module: a second session.start (resume, clear) must not stack another.
    timer ??= $.clock.every(POLL_MS, async () => {
      if (isOpen) await poll($, cfg)
    })
    return started
  })

  on('command.run', { command: 'prs' }, async $ => {
    await $.ui.open({ id: PANE, title: 'My PRs' })
    isOpen = true
    await poll($, cfg)
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
              {row.repo === cfg.homeRepo ? '' : ` (${row.repo})`}
            </Text>
            {rowLines({ Text }, row, '  ')}
          </Box>
        ))}
      </Box>
    )
  })
}
