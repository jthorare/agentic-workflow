# `news` block

Reads Hacker News or any RSS or Atom feed. It needs no connection and no key.

| Config | Meaning |
|---|---|
| `provider` | `hackernews` (default) or `rss` |
| `feed` | Hacker News: `top` (default), `new`, `best`, `ask`, `show`. RSS: the feed's URL |
| `query` | Keep only items whose title holds this text |
| `limit` | 1 to 200, default 30 |
| `since` | Keep only items posted at or after this date and time |

Outputs: `items[]` (`id`, `title`, `url`, `source`, `score?`, `comments?`, `publishedAt?`) and `count`.

The block reads only the hosts in the profile's `workflow.news.hosts` (default `hacker-news.firebaseio.com`). A host that is
not listed, a loopback address and a redirect to another host fail the node with `SND-GRAPH-102`. A body over 1 MB is refused.
Item text comes from the web: treat it as untrusted wherever it reaches a prompt.

```json
{"provider":"rss","feed":"https://feeds.example.com/security.xml","query":"sqlite","limit":10,"since":"{{trigger.scheduledFor}}"}
```

Add the feed's host first: `workflow.news.hosts: [feeds.example.com]`, then approve the profile.
