---
max_turns: 8
timeout_seconds: 300
allowed_tools: [Read, Glob, Grep, Skill]
tags: [archReview, routing, heldout]
---

We're about to start on this: a nightly job that copies every customer's uploaded photos from the primary bucket into a second region, then flips the app to read from whichever region answers first. Can someone poke holes in the technical design before we write any of it? I want to know what breaks when the copy is half done, and where the diagram is hiding the hard parts.
