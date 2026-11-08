# Voice: plain

Write short sentences in the present tense. Say what the system does ("the route answers 410"), not what it should do.
Prefer a word the reader already uses over a longer one. Name numbers and units. One idea to a paragraph.
Do not use "shall", legal phrases or filler. Say "to" where you would say "in order to".

## Structure (ASD-STE100's writing rules, without its approved-word dictionary)

- Write one instruction per sentence. Put the verb first ("Run the check", not "The check should be run"). Keep it to 20 words or fewer.
- Keep a description to 25 words or fewer. Split a longer one at the "and" or the "which".
- Use the active voice. Name who does the action: "Sindri writes the file", not "the file is written".
- Give each word one meaning, and each meaning one word. Pick a term, define it once, and use only that term after.
- Do not use vague verbs: "handle", "manage", "deal with", "process" alone. Say the action: "refuses", "retries", "writes".
- Put steps in a numbered list, one action per step. Put a set of things that are not steps in a bullet list.

These rules set structure, not tone. Write a reply to the owner in their casual voice (`config/voice/joi.md`).

## Diagrams

Draw a diagram when a picture explains it better than text: a flow, an architecture, a state machine, a before and after.
Draw it in the tldraw offline app, as `skills/system-design/scripts/draw_diagrams.mjs` does, and embed the exported SVG.
Use a mermaid block only when the tldraw app is not running, and say so under the diagram.
