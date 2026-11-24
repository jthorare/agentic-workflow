import { expect, mock, test } from 'claude-code/testing'

const HEAD = 'abc123'
const REQUIRED = ['buildkite/white-check-mark-pr-checks', 'buildkite/database-pr-db-tests']

function pr(over: Record<string, unknown>) {
  return {
    number: 1,
    title: 'fix(schedule): sample (FRN-1)',
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
      { __typename: 'CheckRun', name: 'Cursor Bugbot', status: 'COMPLETED', conclusion: 'SUCCESS' },
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
  pr({ number: 104, headRefName: 'frn-1-sample', reviewThreads: { nodes: [{ isResolved: false }] } }),
] } } }

function ghAndGit(branch: string) {
  return (_: unknown, e: { readonly argv: readonly string[] }) => {
    const stdout = e.argv.includes('graphql')
      ? JSON.stringify(SEARCH)
      : e.argv[0] === 'git'
        ? `${branch}\n`
        : JSON.stringify(REQUIRED)
    return { value: { exitCode: 0, stdout, stderr: '', isStdoutTruncated: false, isStderrTruncated: false } }
  }
}

const BAND = {
  plugin: 'pr-line',
  component: 'PromptHint',
  props: { isDraft: false, isWorking: false, hint: '? for shortcuts' },
} as const

test('the PR shows under the prompt for the session branch', async ($, on) => {
  mock.clock(on)
  on('session.start', (_, e) => ({ cwd: e.cwd }))
  on('session.cwd', () => ({ value: '/' }))
  on('process.run', ghAndGit('frn-1-sample'))

  await $.session.start({ cwd: '/', surface: 'terminal', isInteractive: true })

  for (const surface of ['terminal', 'desktop'] as const) {
    const ui = await $.ui.mount({ ...BAND, surface } as never)
    const text = async (re: RegExp) => (await ui.find({ type: 'Text', text: re }))?.text
    expect(await text(/reply to/)).toBe('CI ✓   bots ✓✓   threads 1 •   -> reply to 1 thread')
    expect(await text(/sample/)).toBeUndefined()
    await ui.unmount()
  }
})

test('nothing extra shows under the prompt on main', async ($, on) => {
  mock.clock(on)
  on('session.start', (_, e) => ({ cwd: e.cwd }))
  on('session.cwd', () => ({ value: '/' }))
  on('process.run', ghAndGit('main'))

  await $.session.start({ cwd: '/', surface: 'terminal', isInteractive: true })

  const ui = await $.ui.mount({ ...BAND, surface: 'terminal' } as never)
  expect(await ui.find({ type: 'Text', text: /->/ })).toBeUndefined()
  await ui.unmount()
})
