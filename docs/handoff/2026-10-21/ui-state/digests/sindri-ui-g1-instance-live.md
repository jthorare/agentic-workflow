# instance-live (sindri-ui G1) - done, PR open
Branch feat/sindri-ui-instance-live. Commits: 803f25f27 (red, 20 tests), e4eed45af (green).
Gate: build -warnaserror ok; Ui.Tests unit 2130/2130; Ui.Tests integration 672 total, 0 failed, 662 ran; Architecture 128/128; scoped format clean (Ui, Ui.Testing, Ui.Tests). Rendering not run (no view changes).
Design: IAsyncEnumerable InstanceChanges on ISindriApi (shares Frames() reader with Events); follower WatchInstances (ControlPlaneFollower.Instances.cs): one unfiltered derived stream, open signal then re-read all, 250 ms trailing coalescing per instance, HealthViewModel.ReadInstances retries (3, 200ms doubling). Frame.Catch / Workflows.Catch compare state.
Left: nothing known. Deviation: one unfiltered stream (no &instance= opt-in) serves both list and frame.
