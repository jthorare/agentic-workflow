# sindri-ui L1 digest
State: built, gated, pushed, PR opened. Branch feat/sindri-ui-l1-agent-step (3 commits on origin/main e9465d248).
Gate: build -warnaserror (Ui, Ui.Desktop, Ui.Tests); Ui.Tests unit 2030 pass; Ui.Rendering.Tests 21; Architecture.Tests 128; AgentRunTests+AgentAttachTests 29; InstanceFramesTests 13, AgentRunFrameTests 1 (integration, run once); format verify clean on touched files.
Left: Agents row -> Step link (L2). Events are read only while the frame has seen an agent Step for that instance. Swap UI-side fixtures (AgentRunFixtures) for P8's agent-run.json and api's items-id/live-session.json goldens once on main.
