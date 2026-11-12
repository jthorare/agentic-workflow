# First-run frames: guesses and what is decided (#3236, asset branch pr-assets/first-run-3019)

Decided (spec bd1299c55e plus ideas' check of 7931c3baa3):
- Hello-sindri steps: Start, Make a greeting, Tell me, Add an item. The failure frames say "hello-sindri failed at Make a greeting: the step ran out of time after 60 s." (60 s is the code-step timeout in `CodeBlock.cs:13`.)
- Still stopping uses pid 48213 and the log path `~/.agentic-workflow/sindri-app/logs/serve.log` (`ServeLauncher.cs:11`).
- Didn't start and Didn't come back use the reason "port 7410 is already in use".
- The device-code row shows "the code works for 14:32 more" (the spec adds it, from GitHub's `expires_in`).
- The Linear paste button is "Save key"; GitHub and Slack say "Save token".
- Done on the Accounts page turns primary once no required row is left (GitHub connected); Linear and Slack are optional.
- `known` repos: "you worked on this repo on GitHub in the last 30 days".

Still guesses (invented to fill a frame, not in the spec):
- Linear's "connected as ada" and "ada@ledgerly.app" account names.
- The expired Linear sign-in shows "expired 3h ago".
- The "A new run started" fixture, Weekday check (run 7).
- Home's "Run a first workflow" and "Getting a first workflow ready…" body sentences under the title.
- The Access-denied, keychain-locked and invalid_auth reasons in the row-state frames.
- The scan-failed reason "permission denied" and the 11.8 s and 4.2 s waits.
- The "failed 1m ago" age on the hello-sindri failed row.
