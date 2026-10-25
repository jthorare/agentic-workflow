# Dispatch conventions (no hook enforces these — read before writing a brief)

- **One-shot work** (a bounded task with a single expected result) → dispatch
  as a background subagent, not a long-lived teammate. It reports once and is done.
- **Keep-alive work** (an ongoing collaborator you expect to message again) →
  a named teammate, explicitly marked `keep_alive` in your own brief text to
  yourself (there is no field the platform tracks this in — say so in the
  brief so a human or a future hook can find it).
- **Shut down finished teammates.** A teammate that's done and isn't
  `keep_alive` should be told to stop, not left idling and pinging.

The `idle_notification` count in a session's transcript is how you'll see
whether this discipline is slipping.
