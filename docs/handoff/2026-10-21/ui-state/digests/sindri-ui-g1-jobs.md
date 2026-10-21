# sindri-ui-jobs digest (builder, 2026-10-20)
Worktree /home/thor/repos/aw-wt/sindri-ui-jobs, branch feat/sindri-ui-jobs (merged origin/main 9e4a5313f after commit 97a908ab7).
Commits: 214dafb32 API layer (Api/Jobs, client, fake, contract+golden tests); 97a908ab7 Jobs page + job sheet + shell wiring (PageId.Jobs not a tab, G J, palette, System link, job/<id> sheet).
Gate so far (pre-merge): build -warnaserror ok; Ui.Tests unit 2171 pass; Ui.Tests integration 682 (672 pass, 10 macOS skips).
Left: after merge rerun Ui.Tests unit+integration, Rendering.Tests, Architecture.Tests, scoped format; open PR "feat(ui): Jobs page and job sheet".
Decisions for lead: Jobs is a page, not a 8th tab (7 tabs must fit 900 px); reached by G J, palette, System page link. Shared files touched minimally: Labels.App.cs (Page/KindNoun/ArrivesWith), ShellText.cs (2 props), ShellViewModel.cs (_pageProperties), ShellViewModel.Agents.cs (calls InitJobs, since ShellViewModel.cs is at the 300-line cap), ShellViewModel.AgentSheet.cs (NamedSubject), Palette _pages. Sheet header (shared) still shows the raw job id as SheetSubject (same as sessions); B4's.
No kind filter / state filter on the page (read is unfiltered; JobQuery supports it). RefreshContextMap renders by humanised served kind.
PR #472 open, head b8912285e. Gate: build 0 warn; Ui unit 2173; Ui integration 683 (673 pass, 10 macOS skips); Rendering 21; Architecture 128; scoped format clean. Nothing left but review.
Round 1 review fixes: red tests cafa69ab2; fix 2f8346b5c (J1,J2,J4,J5,J7); merged origin/main e415dbf82 (head e415dbf82, pushed). Gate: build 0 err; Ui unit 2182; Ui integration 683 (673 pass, 10 macOS skips); Rendering 21; Architecture 128; scoped format clean. J3/J6 resolved in writing. Nothing left.
