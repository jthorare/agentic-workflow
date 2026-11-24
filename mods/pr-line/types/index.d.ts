export type Tone = 'success' | 'error' | 'warning' | 'subtle'

export type Person = {
  login: string
  mark: 'approved' | 'changes' | 'requested'
  isOld: boolean
}

export type PrRow = {
  key: string
  number: number
  repo: string
  branch: string
  title: string
  url: string
  ci: { state: 'pass' | 'fail' | 'running' | 'none'; failing: string[]; done: number; total: number }
  bots: { name: string; isOnHead: boolean }[] | null
  threads: number
  isNewThread: boolean
  people: Person[]
  next: string
  nextTone: Tone
}

export type Snapshot = {
  rows: PrRow[]
  updatedAt: number | null
  error: string | null
}

declare module 'claude-code' {
  interface PluginState {
    'pr-line': { snapshot: Shaped<Snapshot>; branch: string | null; repo: string | null }
  }
}
