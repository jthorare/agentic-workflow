import type { Person, PrRow, Tone } from '../types'

export const QUERY = `query {
  search(query: "is:pr is:open author:@me archived:false sort:updated-desc", type: ISSUE, first: 30) {
    nodes {
      ... on PullRequest {
        number title url isDraft mergeable headRefName headRefOid reviewDecision
        repository { nameWithOwner }
        autoMergeRequest { enabledAt }
        mergeQueueEntry { position }
        commits(last: 1) { nodes { commit { statusCheckRollup {
          contexts(first: 100) { nodes {
            __typename
            ... on CheckRun { name status conclusion }
            ... on StatusContext { context state }
          } }
        } } } }
        reviewThreads(first: 100) { nodes { isResolved } }
        latestReviews(first: 30) { nodes { state author { __typename login } commit { oid } } }
        reviewRequests(first: 20) { nodes { requestedReviewer {
          __typename ... on User { login } ... on Team { name } ... on Bot { login }
        } } }
      }
    }
  }
}`

type Bot = { name: string; reviewLogin?: string; checkRun?: string }

// What the mod's options say about the repos it watches. Every field is empty unless the person sets it.
export type ModConfig = {
  // Bot reviewers the ship sequence waits on, per repo (owner/name).
  bots: Record<string, Bot[]>
  // Check runs that belong to bots or deploy previews: left out of the CI count.
  botCheckRuns: Set<string>
  // Prefixes cut from check names in the "CI ✗ ..." text.
  stripPrefixes: string[]
  // The pane's home repo: its rows show no "(owner/name)" suffix.
  homeRepo: string | null
}

const list = (v: unknown): string[] =>
  typeof v === 'string' ? v.split(',').map(x => x.trim()).filter(x => x !== '') : []

// Options (all strings, see each plugin.json): `bots` is JSON like
// {"owner/name":[{"name":"Lint bot","reviewLogin":"lint-bot"},{"name":"Scan","checkRun":"Scan"}]};
// `botCheckRuns` and `stripPrefixes` are comma lists; `homeRepo` is owner/name.
export function readConfig(options: Readonly<Record<string, unknown>>): ModConfig {
  let bots: Record<string, Bot[]> = {}
  try {
    const parsed: unknown = JSON.parse(typeof options.bots === 'string' && options.bots !== '' ? options.bots : '{}')
    if (parsed && typeof parsed === 'object' && !Array.isArray(parsed)) bots = parsed as Record<string, Bot[]>
  } catch {
    // A bad `bots` value means no bots, not a broken mod.
  }
  const botCheckRuns = new Set(list(options.botCheckRuns))
  for (const repoBots of Object.values(bots)) {
    for (const b of Array.isArray(repoBots) ? repoBots : []) if (b.checkRun) botCheckRuns.add(b.checkRun)
  }
  const home = typeof options.homeRepo === 'string' ? options.homeRepo.trim() : ''
  return { bots, botCheckRuns, stripPrefixes: list(options.stripPrefixes), homeRepo: home === '' ? null : home }
}

type Context =
  | { __typename: 'CheckRun'; name: string; status: string; conclusion: string | null }
  | { __typename: 'StatusContext'; context: string; state: string }

type Pr = {
  number: number
  title: string
  url: string
  isDraft: boolean
  mergeable: string
  headRefName: string
  headRefOid: string
  reviewDecision: string | null
  repository: { nameWithOwner: string }
  autoMergeRequest: unknown
  mergeQueueEntry: { position: number } | null
  commits: { nodes: { commit: { statusCheckRollup: { contexts: { nodes: Context[] } } | null } }[] }
  reviewThreads: { nodes: { isResolved: boolean }[] }
  latestReviews: {
    nodes: { state: string; author: { __typename: string; login: string } | null; commit: { oid: string } | null }[]
  }
  reviewRequests: {
    nodes: { requestedReviewer: { __typename: string; login?: string; name?: string } | null }[]
  }
}

export type ThreadMemory = Record<string, { count: number; raisedAt: number }>

const NEW_THREAD_MS = 10 * 60 * 1000

function contextName(c: Context): string {
  return c.__typename === 'CheckRun' ? c.name : c.context
}

function contextState(c: Context): 'pass' | 'fail' | 'running' {
  if (c.__typename === 'CheckRun') {
    if (c.status !== 'COMPLETED') return 'running'
    return ['SUCCESS', 'NEUTRAL', 'SKIPPED'].includes(c.conclusion ?? '') ? 'pass' : 'fail'
  }
  if (c.state === 'SUCCESS') return 'pass'
  if (c.state === 'PENDING' || c.state === 'EXPECTED') return 'running'
  return 'fail'
}

function ciOf(contexts: Context[], required: string[] | undefined, cfg: ModConfig): PrRow['ci'] {
  const ci = contexts.filter(c => !(c.__typename === 'CheckRun' && cfg.botCheckRuns.has(c.name)))
  const byName = new Map(ci.map(c => [contextName(c), contextState(c)]))
  // With the repo's required list, a required check that has not reported yet counts as running.
  const names = required && required.length > 0 ? required : [...byName.keys()]
  if (names.length === 0) return { state: 'none', failing: [], done: 0, total: 0 }
  const states = names.map(n => byName.get(n) ?? 'running')
  const failing = names.filter((_, i) => states[i] === 'fail').map(n => cfg.stripPrefixes.reduce((name, p) => (name.startsWith(p) ? name.slice(p.length) : name), n))
  const done = states.filter(s => s !== 'running').length
  const state = failing.length > 0 ? 'fail' : done < names.length ? 'running' : 'pass'
  return { state, failing, done, total: names.length }
}

function peopleOf(pr: Pr): Person[] {
  const people: Person[] = []
  for (const r of pr.latestReviews.nodes) {
    if (!r.author || r.author.__typename !== 'User') continue
    if (r.state !== 'APPROVED' && r.state !== 'CHANGES_REQUESTED') continue
    people.push({
      login: r.author.login,
      mark: r.state === 'APPROVED' ? 'approved' : 'changes',
      isOld: r.commit?.oid !== pr.headRefOid,
    })
  }
  for (const req of pr.reviewRequests.nodes) {
    const who = req.requestedReviewer
    if (!who || who.__typename === 'Bot') continue
    const login = who.login ?? who.name ?? '?'
    if (!people.some(p => p.login === login)) people.push({ login, mark: 'requested', isOld: false })
  }
  return people
}

function nextOf(
  pr: Pr,
  ci: PrRow['ci'],
  bots: PrRow['bots'],
  threads: number,
  people: Person[],
): { next: string; nextTone: Tone } {
  if (pr.isDraft) return { next: 'draft', nextTone: 'subtle' }
  if (pr.mergeQueueEntry) return { next: `queued, position ${pr.mergeQueueEntry.position}`, nextTone: 'success' }
  if (pr.mergeable === 'CONFLICTING') return { next: 'resolve conflicts', nextTone: 'error' }
  if (ci.state === 'fail') return { next: 'fix CI', nextTone: 'error' }
  const blocker = people.find(p => p.mark === 'changes')
  if (blocker) return { next: `${blocker.login} asked for changes`, nextTone: 'error' }
  if (ci.state === 'running') return { next: 'waiting on CI', nextTone: 'subtle' }
  const missing = (bots ?? []).filter(b => !b.isOnHead)
  if (missing.length > 0) return { next: `${missing.map(b => b.name).join(', ')} not on HEAD yet`, nextTone: 'subtle' }
  if (threads > 0) return { next: `reply to ${threads} thread${threads === 1 ? '' : 's'}`, nextTone: 'warning' }
  const isArmed = pr.autoMergeRequest != null
  const needsApproval = pr.reviewDecision === 'REVIEW_REQUIRED'
  if (isArmed) return { next: needsApproval ? 'armed, waiting on approval' : 'armed', nextTone: 'success' }
  return { next: 'ready for /review', nextTone: 'warning' }
}

export function toRows(
  json: unknown,
  required: Record<string, string[]>,
  memory: ThreadMemory,
  now: number,
  cfg: ModConfig,
): { rows: PrRow[]; memory: ThreadMemory } {
  const nodes = ((json as { data?: { search?: { nodes?: Pr[] } } }).data?.search?.nodes ?? []).filter(
    n => n && typeof n.number === 'number',
  )
  const nextMemory: ThreadMemory = {}
  const rows = nodes.map(pr => {
    const repo = pr.repository.nameWithOwner
    const key = `${repo}#${pr.number}`
    const contexts = pr.commits.nodes[0]?.commit.statusCheckRollup?.contexts.nodes ?? []
    const ci = ciOf(contexts, required[repo], cfg)
    const bots = cfg.bots[repo]
      ? cfg.bots[repo].map(b => ({
          name: b.name,
          isOnHead: b.reviewLogin
            ? pr.latestReviews.nodes.some(r => r.author?.login === b.reviewLogin && r.commit?.oid === pr.headRefOid)
            : contexts.some(
                c => c.__typename === 'CheckRun' && c.name === b.checkRun && c.status === 'COMPLETED',
              ),
        }))
      : null
    const threads = pr.reviewThreads.nodes.filter(t => !t.isResolved).length
    const before = memory[key]
    const raisedAt = before && threads > before.count ? now : (before?.raisedAt ?? 0)
    nextMemory[key] = { count: threads, raisedAt }
    const people = peopleOf(pr)
    return {
      key,
      number: pr.number,
      repo,
      branch: pr.headRefName,
      title: pr.title,
      url: pr.url,
      ci,
      bots,
      threads,
      isNewThread: threads > 0 && now - raisedAt < NEW_THREAD_MS,
      people,
      ...nextOf(pr, ci, bots, threads, people),
    }
  })
  return { rows, memory: nextMemory }
}
