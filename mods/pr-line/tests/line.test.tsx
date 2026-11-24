import { expect, mock, test } from 'claude-code/testing'

const HEAD = 'abc123'
const OPTIONS = {
  bots: JSON.stringify({
    'example-org/app': [
      { name: 'review-bot', reviewLogin: 'review-bot' },
      { name: 'scan-bot', checkRun: 'Scan Bot' },
    ],
  }),
  botCheckRuns: 'Preview Deploy',
  stripPrefixes: 'ci/',
  homeRepo: 'example-org/app',
}
const REQUIRED = ['ci/lint', 'ci/unit']

function pr(over: Record<string, unknown>) {
  return {
    number: 1,
    title: 'fix: sample (ABC-1)',
    url: 'https://github.com/example-org/app/pull/1',
    isDraft: false,
    mergeable: 'MERGEABLE',
    headRefName: 'other-branch',
    headRefOid: HEAD,
    reviewDecision: 'REVIEW_REQUIRED',
    repository: { nameWithOwner: 'example-org/app' },
    autoMergeRequest: null,
    mergeQueueEntry: null,
    commits: { nodes: [{ commit: { statusCheckRollup: { contexts: { nodes: [
      { __typename: 'StatusContext', context: REQUIRED[0], state: 'SUCCESS' },
      { __typename: 'StatusContext', context: REQUIRED[1], state: 'SUCCESS' },
      { __typename: 'CheckRun', name: 'Scan Bot', status: 'COMPLETED', conclusion: 'SUCCESS' },
    ] } } } }] },
    reviewThreads: { nodes: [] },
    latestReviews: { nodes: [
      { state: 'COMMENTED', author: { __typename: 'Bot', login: 'review-bot' }, commit: { oid: HEAD } },
    ] },
    reviewRequests: { nodes: [] },
    ...over,
  }
}

const SEARCH = { data: { search: { nodes: [
  pr({ number: 101, autoMergeRequest: { enabledAt: 'x' } }),
  pr({
    number: 102,
    commits: { nodes: [{ commit: { statusCheckRollup: { contexts: { nodes: [
      { __typename: 'StatusContext', context: REQUIRED[0], state: 'FAILURE' },
    ] } } } }] },
  }),
  pr({
    number: 103,
    reviewThreads: { nodes: [{ isResolved: false }, { isResolved: true }] },
    latestReviews: { nodes: [
      { state: 'APPROVED', author: { __typename: 'User', login: 'alex' }, commit: { oid: 'older' } },
      { state: 'CHANGES_REQUESTED', author: { __typename: 'User', login: 'sam' }, commit: { oid: HEAD } },
    ] },
    reviewRequests: { nodes: [{ requestedReviewer: { __typename: 'Team', name: 'eng' } }] },
  }),
  pr({ number: 104, headRefName: 'abc-1-sample', reviewThreads: { nodes: [{ isResolved: false }] } }),
] } } }

function ghAndGit(branch: string, origin: string | null = 'https://github.com/example-org/app.git', search: unknown = SEARCH) {
  return (_: unknown, e: { readonly argv: readonly string[] }) => {
    const done = (stdout: string, exitCode = 0) => ({
      value: { exitCode, stdout, stderr: '', isStdoutTruncated: false, isStderrTruncated: false },
    })
    if (e.argv.includes('graphql')) return done(JSON.stringify(search))
    if (e.argv[0] === 'git' && e.argv[1] === 'remote') return origin === null ? done('', 2) : done(`${origin}\n`)
    if (e.argv[0] === 'git') return done(`${branch}\n`)
    return done(JSON.stringify(REQUIRED))
  }
}

const BAND = {
  plugin: 'pr-line',
  component: 'PromptHint',
  props: { isDraft: false, isWorking: false, hint: '? for shortcuts' },
} as const

test('the PR shows under the prompt for the session branch', { options: OPTIONS }, async ($, on) => {
  mock.clock(on)
  on('session.start', (_, e) => ({ cwd: e.cwd }))
  on('session.cwd', () => ({ value: '/' }))
  on('process.run', ghAndGit('abc-1-sample'))

  await $.session.start({ cwd: '/', surface: 'terminal', isInteractive: true })

  for (const surface of ['terminal', 'desktop'] as const) {
    const ui = await $.ui.mount({ ...BAND, surface } as never)
    const text = async (re: RegExp) => (await ui.find({ type: 'Text', text: re }))?.text
    expect(await text(/reply to/)).toBe('CI ✓   bots ✓✓   threads 1 •   -> reply to 1 thread')
    expect(await text(/sample/)).toBeUndefined()
    await ui.unmount()
  }
})

test('nothing extra shows under the prompt on main', { options: OPTIONS }, async ($, on) => {
  mock.clock(on)
  on('session.start', (_, e) => ({ cwd: e.cwd }))
  on('session.cwd', () => ({ value: '/' }))
  on('process.run', ghAndGit('main'))

  await $.session.start({ cwd: '/', surface: 'terminal', isInteractive: true })

  const ui = await $.ui.mount({ ...BAND, surface: 'terminal' } as never)
  expect(await ui.find({ type: 'Text', text: /->/ })).toBeUndefined()
  await ui.unmount()
})

const TWO_REPOS = { data: { search: { nodes: [
  // Most recently updated first: the other repo's PR on the same branch name comes first.
  pr({ number: 7, headRefName: 'shared-name', repository: { nameWithOwner: 'other-org/lib' },
    reviewThreads: { nodes: [{ isResolved: false }, { isResolved: false }] } }),
  pr({ number: 8, headRefName: 'shared-name', reviewThreads: { nodes: [{ isResolved: false }] } }),
] } } }

async function drawn($: any, on: any, origin: string | null) {
  mock.clock(on)
  on('session.start', (_: unknown, e: { cwd: string }) => ({ cwd: e.cwd }))
  on('session.cwd', () => ({ value: '/' }))
  on('process.run', ghAndGit('shared-name', origin, TWO_REPOS))
  await $.session.start({ cwd: '/', surface: 'terminal', isInteractive: true })
  const ui = await $.ui.mount({ ...BAND, surface: 'terminal' } as never)
  const text = (await ui.find({ type: 'Text', text: /->/ }))?.text
  await ui.unmount()
  return text
}

test('the same branch name open in two repos shows only the session repo PR', { options: OPTIONS }, async ($, on) => {
  // example-org/app has 1 open thread; other-org/lib (listed first) has 2.
  expect(await drawn($, on, 'git@github.com:example-org/app.git')).toBe('CI ✓   bots ✓✓   threads 1 •   -> reply to 1 thread')
})

test('a session with no remote shows no line', { options: OPTIONS }, async ($, on) => {
  expect(await drawn($, on, null)).toBeUndefined()
})

test('the line says (stale) when the last gh poll failed', { options: OPTIONS }, async ($, on) => {
  const clock = mock.clock(on)
  on('session.start', (_, e) => ({ cwd: e.cwd }))
  on('session.cwd', () => ({ value: '/' }))
  let calls = 0
  const ok = ghAndGit('abc-1-sample')
  on('process.run', (h: unknown, e: { readonly argv: readonly string[] }) => {
    if (e.argv.includes('graphql') && ++calls > 1) {
      return { value: { exitCode: 1, stdout: '', stderr: 'rate limited', isStdoutTruncated: false, isStderrTruncated: false } }
    }
    return ok(h, e)
  })
  await $.session.start({ cwd: '/', surface: 'terminal', isInteractive: true })
  await clock.advance(61_000)
  const ui = await $.ui.mount({ ...BAND, surface: 'terminal' } as never)
  expect(await ui.find({ type: 'Text', text: /\(stale\)/ })).toBeDefined()
  await ui.unmount()
})

test('a second session.start does not stack a second poll timer', { options: OPTIONS }, async ($, on) => {
  const clock = mock.clock(on)
  on('session.start', (_, e) => ({ cwd: e.cwd }))
  on('session.cwd', () => ({ value: '/' }))
  const ok = ghAndGit('abc-1-sample')
  let branchReads = 0
  on('process.run', (h: unknown, e: { readonly argv: readonly string[] }) => {
    if (e.argv[0] === 'git' && e.argv[1] === 'branch') branchReads++
    return ok(h, e)
  })
  await $.session.start({ cwd: '/', surface: 'terminal', isInteractive: true })
  await $.session.start({ cwd: '/', surface: 'terminal', isInteractive: true })
  const before = branchReads
  await clock.advance(60_000)
  // One timer firing reads the branch once.
  expect(branchReads - before).toBe(1)
})
