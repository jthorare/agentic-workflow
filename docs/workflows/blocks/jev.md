# `jev` block

Asks Jev (the TypeSafe API the judge uses, `POST https://api.typesafe.ai/v1/systemone`) questions about a text.

| Config | Meaning |
|---|---|
| `text` | What the questions are about |
| `questions` | A list of `{name, type, instructions, criteria}` |
| `model` | The Jev model. Default: the judge's pinned model, `jev-1.13.0` |

Question types: `choice` (`criteria` maps each option to what it means, up to 255), `score` (`criteria` lists the levels, lowest
first, 2 to 10) and `noul` (a yes/no as a number; no `criteria`).

Outputs: one field per question, named by `name`: `{type, choice | score | noul, confidence, probabilities}`; plus `model`
and `usage` (`input_tokens`, `output_tokens`).

Jev is a third party. The block redacts the text, the instructions and the criteria (`Aw.Redaction`) and cuts the request to
160,000 characters before it leaves the machine. The key is `TYPESAFE_API_KEY` in the environment, else the judge's keychain entry, so
one key serves both. A failure carries the judge's reason code: `no-api-key`, `network-error`, `http-NNN`, `timeout` or
`unparseable-answer`.

```json
{"text":"{{search.facts}}","questions":[{"name":"mood","type":"choice","instructions":"How does the writer feel?","criteria":{"calm":"relaxed","angry":"upset"}}]}
```
