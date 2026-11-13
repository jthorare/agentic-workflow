using System.Globalization;
using System.Text;
using System.Text.Json.Nodes;
using Aw.Kernel;
using Aw.Kernel.Json;
using Aw.Kernel.Testing;
using Aw.Sindri.Ui.Api;
using Aw.Sindri.Ui.Api.CodeIndex;
using Aw.Sindri.Ui.Api.Connections;
using Aw.Sindri.Ui.Api.Context;
using Aw.Sindri.Ui.Api.Jobs;
using Aw.Sindri.Ui.Api.Linear;
using Aw.Sindri.Ui.Api.Memory;
using Aw.Sindri.Ui.Api.Projects;
using Aw.Sindri.Ui.Api.Resources;
using Aw.Sindri.Ui.Api.Reviews;
using Aw.Sindri.Ui.Api.Sessions;
using Aw.Sindri.Ui.Api.Setup;
using Aw.Sindri.Ui.Api.Sources;
using Aw.Sindri.Ui.Api.Workflows;
using Aw.Sindri.Ui.Testing;

namespace Aw.Sindri.Ui.Tests.Api;

/// <summary>
/// UI spec E-17: every API golden reads through the app's own JSON context, so a wire change in Sindri fails this unit
/// lane, not a prover run. A golden is a record the app reads, a ProblemDetails refusal, or a route the app never calls.
/// The goldens are Sindri's own (tests/Aw.Sindri.Tests/Golden/Api, linked into this test output as Golden/Api), plus
/// U1's spec-shaped stand-ins for P2's routes (Golden/SpecShaped) until P2 writes them.
/// </summary>
[Lane(TestLane.Unit)]
public sealed class GoldenContractTests
{
    private static readonly Uri _api = new("http://127.0.0.1:7410/");

    // The routes the app reads, by slug, and how; a new slug fails until it is placed here or in _notRead.
    private static readonly Dictionary<string, Func<byte[], Result<object>>> _read = new(StringComparer.Ordinal)
    {
        ["ping"] = As<PingView>,
        ["status"] = As<StatusView>,
        ["events"] = AsEvents,
        ["commands"] = As<CommandList>,
        ["services"] = As<ServiceList>,
        ["views-rebuild"] = As<CommandAcceptedView>,
        ["queue"] = As<QueueView>,
        ["items-id"] = As<ItemView>,
        ["items-id-timeline"] = As<TimelineView>,
        ["items"] = As<ItemCreatedView>,   // POST /items' created* goldens only
        ["items-id-close"] = As<CommandAcceptedView>,
        ["code-index-status"] = As<CodeIndexStatusView>,   // the Health card's Code index lines (CI-10b)
        ["needs-you"] = As<NeedsYouList>,
        ["decisions"] = As<DecisionList>,
        ["decisions-id"] = As<CommandAcceptedView>,   // POST /decisions/{id}'s accepted goldens; the refusals are ProblemDetails
        ["requests"] = AsRequests,
        ["sessions"] = As<SessionList>,   // GET /sessions (collector CO-3); the 503 SND-SESSION-002 bodies are ProblemDetails
        ["sessions-id"] = As<SessionView>,   // GET /sessions/{provider}/{id}; the 404 is ProblemDetails
        ["context-lenses"] = As<LensesView>,
        ["context-concepts"] = As<ContextConceptsView>,   // GET /sindri/context/concepts: the Context page's concept band (ui #1012); the refusals are ProblemDetails
        ["context-concepts-glossary"] = As<ConceptGlossaryView>,   // GET /sindri/context/concepts/glossary: the concept cards' top terms (ui #1036); no-map-030 is a ProblemDetails
        ["context-pins"] = As<PinsView>,   // GET /sindri/context/pins: the live pins against the approved profile and the map, and the rebuild state
        ["context-pins-suggest"] = As<PinSuggestView>,   // GET /sindri/context/pins/suggest: the "Pin this" and "Add several" suggestions; invalid-179 is a ProblemDetails
        ["workflows-blocks"] = As<WorkflowBlockList>,   // GET /sindri/workflows/blocks: the editor's block form (#2258 PR 17 part 2b)
        ["workflows-slug-ops"] = As<WorkflowOpsApplied>,   // POST /workflows/{slug}/ops: ops-applied; ops-stale and ops-missing-fields are ProblemDetails
        ["workflows-slug-activate"] = As<WorkflowActivated>,   // POST /workflows/{slug}/activate: activated; not-found is a ProblemDetails
        ["context-whole"] = As<WholeMapView>,   // GET /context/map/whole (whole-map spec §2.1); the refusals (006, 030, 044, 045, 120, 200) are ProblemDetails
        ["context-filemap"] = As<FileMapView>,
        ["context-code"] = As<CodeView>,   // GET /sindri/context/code: the code lens' callers and callees (S-9)
        ["context-map"] = As<MapStatusView>,
        ["context-map-publish"] = As<MapPublishView>,
        ["context-pages"] = As<PagesView>,   // GET /context/pages: the page sheet's reads, and q for Pick another (pages spec §3.6)
        ["context-pages-preview"] = As<PagesPreview>,   // QUERY /context/pages/preview (Add several pages); invalid-259 is ProblemDetails
        ["context-map-promote"] = As<PromotePreview>,
        ["context-map-review"] = AsReview,
        ["context-map-review-history"] = As<ReviewHistoryView>,
        ["context-node"] = As<NodeView>,
        ["items-id-open"] = As<OpenAcceptedView>,   // POST /items/{id}/open: the accepted answer (the refusals are ProblemDetails)
        ["jobs"] = As<JobList>,   // GET /jobs: the Jobs page's list; the 400 SND-CLI-003 bodies are ProblemDetails
        ["jobs-id"] = As<JobView>,   // GET /jobs/{id}: the job sheet; the 404 is ProblemDetails
        ["workflows"] = As<WorkflowList>,   // GET /workflows: the Workflows page's list (#2258 PR 17 part 1); the refusals are ProblemDetails
        ["workflows-slug"] = As<WorkflowView>,   // GET /workflows/{slug}: the runs header; the spec stays raw
        ["workflows-slug-pause"] = As<WorkflowPaused>,   // POST /workflows/{slug}/pause (the Pause button); the success is paused.json
        ["workflows-slug-runs"] = As<WorkflowRunList>,   // GET /workflows/{slug}/runs: a workflow's runs
        ["workflow-runs-run"] = As<WorkflowRunView>,   // GET /workflow-runs/{run}: a run's detail
        ["workflow-runs-run-cancel"] = As<WorkflowRunCancelled>,   // POST /workflow-runs/{run}/cancel's cancelled body (the API golden cancels a queued run)
        ["workflow-runs-run-nodes-node-takeover"] = As<AgentNodeControlled>,   // POST /workflow-runs/{run}/nodes/{node}/takeover's taken-over body; the refusals are ProblemDetails
        ["workflow-runs-run-nodes-node-handback"] = As<AgentNodeControlled>,   // POST .../handback's handed-back body; the refusals are ProblemDetails
        ["jobs-id-cancel"] = As<CommandAcceptedView>,   // POST /jobs/{id}/cancel's accepted answer; the refusals are ProblemDetails
        ["projects"] = As<ProjectList>,   // GET /projects: the Projects page's three groups (projects page spec §6.2)
        ["reviews"] = As<ReviewList>,   // GET /reviews: the Reviews tab, its badge and Home's waiting line (review page spec §6.1)
        ["reviews-login"] = As<ReviewLoginSaved>,   // POST /reviews/login: the Reviews tab's login Save (review page spec §6.1); the refusals are ProblemDetails
        ["reviews-fix"] = As<ReviewFixSent>,   // POST /reviews/{owner}/{repo}/{number}/fix: Send as fix task (review page spec §8.2); the 403, 404, 409 refusals are ProblemDetails
        ["reviews-pr"] = As<Ui.Api.Reviews.ReviewPage>,   // GET /reviews/{owner}/{repo}/{number}: the review page (review page spec §6.2); the 404, 409 refusals are ProblemDetails
        ["review-settings"] = As<ReviewSettingsRead>,   // GET /reviews/settings?project=: the project sheet's Review settings (Ph9, hand-written until the route lands)
        ["review-settings-save"] = As<ReviewSettingsSaved>,   // PUT /reviews/settings: Save and approve (Ph9); the 400 SND-REVIEW-001/002 and 403 refusals are ProblemDetails
        ["library-roots"] = As<LibraryRootChanged>,   // POST /library/roots, DELETE /library/roots/{name}, POST /library/aw: Settings -> Library's three writes (review page spec §6.3); the refusals are ProblemDetails
        ["library"] = As<LibraryView>,   // GET /library: the Reviews header's library errors sheet (review page spec §6.1)
        ["projects-linear"] = As<ProjectLinearList>,   // GET /projects/linear: the Add project sheet's From Linear rows (projects page spec §3.2)
        ["linear-issues"] = As<LinearIssueList>,   // GET /linear/issues: Work → Linear's list (Linear hub spec §3, Ph3 1a; hand-written until api serves it)
        ["linear-issue"] = As<LinearIssueOne>,   // GET /linear/issues/{key}: one issue with its comments (hand-written until api serves it)
        ["linear-lookups"] = As<LinearLookups>,   // GET /linear/lookups/{team}: a team's states, members, labels and cycles (hand-written until api serves it)
        ["linear-since"] = As<LinearSince>,   // GET /linear/since: the changes strip above the filter bar (hand-written until api serves it)
        ["linear-change"] = As<LinearChangeApplied>,   // POST /linear/issues/{key}/change's 200 (Linear hub 1b); the 409, 504, 429 and refusals are ProblemDetails with extras (read in SindriApiClientLinearTests)
        ["linear-comment"] = As<LinearCommentPosted>,   // POST /linear/issues/{key}/comments' 200
        ["linear-create"] = As<LinearCreated>,   // POST /linear/issues' 201
        ["linear-connection"] = As<LinearConnection>,   // GET /linear/connection: Settings, Connections, Linear
        ["linear-connection-teams"] = As<LinearTeamChoices>,   // POST /linear/connection/teams
        ["linear-connect"] = As<LinearConnected>,   // POST /linear/connection's saved answers
        ["linear-disconnect-write"] = As<LinearDisconnected>,   // DELETE /linear/connection/write
        ["projects-id"] = As<ProjectOne>,   // GET /projects/{id}: one card with its hidden flag
        ["projects-attach"] = As<ProjectAcceptedView>,   // POST /projects' accepted answer: the new prj_ id at version 1 (hand-written until the route's scenario records it)
        ["projects-id-change"] = As<ProjectAcceptedView>,   // POST /projects/{id}/change's accepted answers (a prj_ id, or a led project's key for its notes); the stale 409 is ProblemDetails
        ["projects-id-detach"] = As<ProjectAcceptedView>,   // POST /projects/{id}/detach's accepted answer
        ["projects-hide"] = As<ProjectAcceptedView>,   // POST /projects/hide's accepted answer
        ["setup"] = As<SetupView>,   // GET /setup (setup-once spec §7, api #458): the Setup card and page; the "extra-" scenarios are the app's own (SpecShaped)
        ["index-status"] = As<IndexStatusView>,   // GET /index/status (#1597, spec-shaped until the context lane's PR lands): the Health Context index card
        ["context-map-refresh"] = As<JobStarted>,   // POST /context/map/refresh's 202 (the setup map row's Retry); the 409 is ProblemDetails
        ["setup-repos"] = As<JobStarted>,   // POST /setup/repos' 200 (api #528, AddRepo); the 400/409/403 bodies are ProblemDetails
        ["setup-mcp"] = As<JobStarted>,   // POST /setup/mcp's 202 (RegisterMcp, spec-shaped)
        ["skills"] = As<SkillsView>,   // GET /skills: the skill blocks the Instance sheet's skill card reads (skill flows spec §8.1)
        ["launch-picks"] = As<LaunchPicksView>,   // GET /launch/picks: the model picks of the runs (model-pick spec §6.1)
        ["launches-id"] = As<LaunchView>,   // GET /launches/{id}: the launch an Open started (Attach, the run line)
        ["profile"] = As<ProfileRead>,   // GET /profile: the approval's plain-words summary is the part the Setup profile step reads (api #782)
        ["sandbox-status"] = As<Ui.Api.Sandbox.SandboxStatusView>,   // GET /sandbox/status: Health and Settings (SB-6; spec-shaped until the server's golden is recorded)
        ["sandbox-check"] = As<Ui.Api.Sandbox.SandboxCheckView>,   // GET /sandbox/check: Check again and the resume list (SB-6, spec-shaped)
        ["sandbox-denials"] = As<Ui.Api.Sandbox.DenialsView>,   // GET /sandbox/denials?item=: the item sheet's Blocked list (SB-6, spec-shaped)
        ["sandbox-allow"] = As<Ui.Api.Sandbox.AllowView>,   // POST /sandbox/allow's allowed answer (it applies on save); the refusals are ProblemDetails (SB-6, spec-shaped)
        ["todos"] = As<TodoListView>,   // GET /todos: the todo panel's list (todo spec Ph2)
        ["todos-add"] = As<TodoAcceptedView>,   // POST /todos' 200 (api Ph1a records it); the refusals are ProblemDetails
        ["todos-change"] = As<TodoAcceptedView>,   // POST /todos/{id}/change's 200, e.g. clear-due (api Ph1a)
        ["todos-done"] = As<TodoAcceptedView>,   // POST /todos/{id}/done's 200 (api Ph1a)
        ["todos-reopen"] = As<TodoAcceptedView>,   // POST /todos/{id}/reopen's 200 (api Ph1a)
        ["todos-remove"] = As<TodoAcceptedView>,   // POST /todos/{id}/remove's 200 (api Ph1a)
        ["todos-restore"] = As<TodoAcceptedView>,   // POST /todos/{id}/restore's 200 (api Ph1a)
        ["todos-note-check"] = As<TodoNoteCheckView>,   // POST /todos/note/check: the Settings card's Check note (todo Ph3b); a bad key is ProblemDetails
        ["todos-note-set"] = As<TodoNoteChangedView>,   // POST /todos/note/set: Start syncing (changed, unchanged); the stale 409 is ProblemDetails
        ["todos-note-stop"] = As<TodoNoteChangedView>,   // POST /todos/note/stop: Stop syncing (changed, unchanged)
        ["todos-note"] = As<TodoNoteView>,   // GET /sindri/todos/note (note, hold); conflict.json is a todo, read as TodoView by _readByScenario
        ["connections"] = As<ConnectionList>,   // GET /sindri/connections; the added-* scenarios are POST's ConnectionAdded (_readByScenario)
        ["connections-id"] = As<ConnectionOne>,   // GET /sindri/connections/{name}; removed is DELETE's ConnectionRemoved (_readByScenario)
        ["connections-id-test"] = As<ConnectionTested>,   // POST /sindri/connections/{name}/test
        ["connections-id-rename"] = As<ConnectionRenamed>,   // POST /sindri/connections/{name}/rename (the Rename action)
        ["connections-id-signin"] = As<ConnectionAdded>,   // POST /sindri/connections/{name}/signin (the Sign in again action)
        ["memories"] = As<MemoryList>,   // GET /sindri/memories; created is POST's MemoryCreated (_readByScenario)
        ["memories-id-search"] = As<MemorySearchView>,   // GET /sindri/memories/{name}/search
        ["memories-id-facts"] = As<MemoryFactWritten>,   // POST /sindri/memories/{name}/facts; the forgot* scenarios are DELETE's MemoryForgotten (_readByScenario)
        ["resources"] = As<ResourceList>,   // GET /sindri/resources: the Resources page's rows (#2258 PR18b); bad-owner is a ProblemDetails
        ["resources-purge"] = As<PurgeResourcesResult>,   // POST /sindri/resources/purge: the purged count; the 409 SND-RES-003 and 400 SND-RES-006 bodies are ProblemDetails
    };

    // A scenario of a slug that holds files of another shape than the rest of its folder (todos-note/conflict.json is the TodoItem GET /todos/{id} serves).
    private static readonly Dictionary<(string Slug, string Scenario), Func<byte[], Result<object>>> _readByScenario = new()
    {
        [("todos-note", "conflict")] = AsShownTodo,   // { asOf, todo }: the app reads the todo inside (the panel takes it from the list, not from this route)
        [("connections", "added-key")] = As<ConnectionAdded>,
        [("connections", "added-oauth")] = As<ConnectionAdded>,
        [("connections-id", "removed")] = As<ConnectionRemoved>,
        [("memories", "created")] = As<MemoryCreated>,
        [("workflows-slug", "archived")] = As<WorkflowArchived>,   // DELETE /workflows/{slug}'s answer; the folder's found is GET's WorkflowView and archive-not-found, replace-stale and not-found are ProblemDetails
        [("workflows-slug-runs", "started")] = As<WorkflowRunStarted>,   // POST /workflows/{slug}/runs' answers (Run now); the folder's list-* are GET's WorkflowRunList and its refusals are ProblemDetails
        [("workflows-slug-runs", "queued")] = As<WorkflowRunStarted>,
        [("workflows-slug-runs", "skipped")] = As<WorkflowRunStarted>,
        [("memories-id-facts", "forgot")] = As<MemoryForgotten>,
        [("memories-id-facts", "forgot-tag")] = As<MemoryForgotten>,
        [("workflows", "created")] = As<WorkflowCreated>,   // POST /workflows: { id, slug, revision, specHash }; not a WorkflowList
        [("workflows-slug", "replaced")] = AsJsonObject,   // PUT /workflows/{slug}: { slug, revision, specHash }, the CLI's and the MCP tools' answer; not a WorkflowView
        [("workflows-slug", "archived")] = AsJsonObject,   // DELETE /workflows/{slug}: { slug, archivedAt }, the CLI's and the MCP tools' answer; not a WorkflowView
    };

    // Routes the app never calls yet: stop is Health's (U4), and the
    // context routes U9 does not read (eval, proposals, query).
    private static readonly HashSet<string> _notRead = new(StringComparer.Ordinal)
    {
        "stop",
        "toolkits", "toolkits-build", "toolkits-name-start", "toolkits-name-stop", "toolkits-name-pause", "toolkits-name-resume", "toolkits-name-calls",   // agent-first PR 16c: the app has no Toolkits page; the CLI's `toolkit` verbs and the toolkit_* MCP tools call these
        "workflow-runs-run-nodes-node-answer",   // #2425: POST .../nodes/{node}/answer, the engine's answer to an ask_owner question; the app calls it once the Needs-you question row has its answer form (UI lane)
        "todos-id",   // the app never calls GET /sindri/todos/{id}; the CLI's `todo show` does
        "todos-keep", "todos-keep-undo", "todos-hold-apply", "todos-hold-undo",   // the picks (todo Ph3b): the conflict and hold rows that call them are ui's Ph4
        "workflow-instances", "workflow-instance", "workflow-instance-fact", "workflow-status", "workflow-recipe", "workflow-recipes", "workflow-recipes-id-stats", "workflow-instances-start", "workflow-recipes-approve-all",   // the app has no Recipes or workflow-instance pages (the Workflows page reads only workflows, workflows-slug, workflows-slug-runs and workflow-runs-run); these routes stay served until the backend PR removes them
        "workflow-challenges-app",   // q93: the app asks for no challenge any more; the route stays served until api removes it
        "setup-repos",   // POST /setup/repos (Add repo): the app's call and the job's progress are U3's (SO-U3)
        "context-concepts-id-members",   // GET /sindri/context/concepts/{id}/members: the band reads sizes from the concepts answer, never a concept's member list
        "context-eval", "context-map-proposals", "context-query", "context-history", "context-subgraph", "context-search",   // not U9's: eval, proposals (U13) and the query routes are read by the phases that call them
        "items",   // GET /items (the list) is never called; POST /items' created* goldens are read (see the loop)
        "handoff",   // POST /sindri/handoff (#2258 PR 12): a local app or script's call; the CLI's `handoff send`, never the app's
        "ledger",   // API P5: the audit read is the CLI's; the app follows the stream and re-queries
        "services-name-run", "services-name-pause", "services-name-resume",   // API P6: the services calls are U4's (Services); the job calls are read below
        "sessions-id-heartbeat", "sessions-id-exit", "sessions-start", "sessions-worktree", "sessions-last-start", "launch-targets", "launch-targets-probe",   // API P10a (U8): U10's session wrap's own calls and the launch-target reads, never the app's
        "workflow-instance-scoping",   // API2 (SC-K2): the scoping stop view; the app reads it in SC-U (U2)
        "workflow-instance-line",   // LN-API1 (LN-L): the line board of a workflow.line instance; the app reads it in LN-U (LN-UI1)
        "workflow-instance-design-stop",   // LN-API2 (LN-K): stop 2's view of a workflow.plan instance; the app reads it in LN-U (LN-UI2)
        "sandbox-status", "sandbox-check", "sandbox-denials", "sandbox-allow",   // SB-6a serves them; the app reads them in SB-6b (Health, Settings, the park card and the item sheet)
        "code-index-symbols", "code-index-callers", "code-index-callees",   // the code-index reads (CI-10a) are the CLI's; status is read below for the Health card (CI-10b)
        "events-catalog",   // API P5 (E-15): the app reads each type's invalidates once it switches from re-querying every view on each event (UI lane)
        "todos-note-import",   // POST /todos/note/import (todo Ph3b): dry-run, imported and the stale 409; the app's Import and start syncing reads it with its own view once the card's golden contract lists it
        "workflows-validate",   // #2258 PR 8b: validate is the CLI's; ops and activate are read above
        "library-doc",   // PR review page Ph5b: GET /sindri/library/{doc} is the CLI's `library show` for now; the app reads it when its doc sheet lands (UI lane)
    };

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    [Fact]
    public void Every_golden_reads_through_the_apps_json_context_or_is_a_refusal()
    {
        var goldens = GoldenFiles.All().ToList();
        Assert.Contains(goldens, g => g.Kind == "Api" && g.Slug == "status");
        Assert.Contains(goldens, g => g.Kind == "SpecShaped");

        foreach (var (kind, slug, scenario, body) in goldens)
        {
            var bytes = Encoding.UTF8.GetBytes(body);
            if (Problem(bytes) is { } problem)
            {
                Assert.StartsWith("SND-", problem.Title, StringComparison.Ordinal);
                Assert.False(string.IsNullOrEmpty(problem.Detail), $"{kind}/{slug}/{scenario}: a refusal without a detail");
                continue;
            }

            if (_notRead.Contains(slug) && !(slug == "items" && scenario.StartsWith("created", StringComparison.Ordinal)))
            {
                continue;
            }

            var placed = _readByScenario.TryGetValue((slug, scenario), out var read) || _read.TryGetValue(slug, out read);
            Assert.True(placed, $"{kind}/{slug}/{scenario}: a route the app doesn't place (read it or list it as not read)");
            var value = read!(bytes);
            Assert.True(value.IsSuccess, $"{kind}/{slug}/{scenario}: {(value.IsError ? value.Error.Message : string.Empty)}");
        }
    }

    [Fact]
    public void Requests_slug_reads_list_scenarios_as_RequestList_and_others_as_RequestFiled()
    {
        var list = AwJson.Deserialize<RequestList>(Encoding.UTF8.GetBytes(GoldenFiles.Api("requests", "list-fixture")), SindriApiClient.Json).Value;
        var filed = AwJson.Deserialize<RequestFiledView>(Encoding.UTF8.GetBytes(GoldenFiles.Api("requests", "filed")), SindriApiClient.Json).Value;

        var servedFiled = GoldenFiles.Node("requests", "filed");
        Assert.Equal(GoldenFiles.Node("requests", "list-fixture")["requests"]!.AsArray().Select(r => (string)r!["state"]!), list.Requests.Select(r => r.State), StringComparer.Ordinal);
        Assert.Equal(((long)servedFiled["accepted"]!["sequence"]!, (string)servedFiled["requestId"]!), (filed.Accepted.Sequence, filed.RequestId));
        Assert.True(AsRequests(Encoding.UTF8.GetBytes(GoldenFiles.Api("requests", "list-empty"))).Value is RequestList);
        Assert.True(AsRequests(Encoding.UTF8.GetBytes(GoldenFiles.Api("requests", "filed"))).Value is RequestFiledView);
    }

    [Fact]
    public void The_services_goldens_carry_why_a_service_is_paused_or_failed_and_what_it_does()
    {
        var paused = Read<ServiceList>("services", "paused").Services!.Single(s => string.Equals(s.Name, "heartbeat", StringComparison.Ordinal));
        var failed = Read<ServiceList>("services", "ran-failed").Services!.Single(s => string.Equals(s.Name, "heartbeat", StringComparison.Ordinal));

        Assert.Equal(("Records that Sindri is alive.", "human:cli", DateTimeOffset.Parse("2026-01-01T00:00:00Z", CultureInfo.InvariantCulture)),
            (paused.Description, paused.PausedBy, paused.PausedAt));
        Assert.Equal(("failed", "SND-SCHED-001", null), (failed.LastRun!.Outcome, failed.LastRun.Code, failed.LastRun.Detail));
    }

    [Fact]
    public void The_status_goldens_carry_every_field_the_health_page_reads()
    {
        // The one running scenario keeps its pre-#2258 name 'running-unapproved'; with no approval its profile is just the live file.
        var running = Read<StatusView>("status", "running-unapproved");

        Assert.Equal((true, 4242, 1L, 1), (running.Running, running.Pid, running.Epoch, running.ApiVersion));
        _ = Assert.NotNull(running.LeaseExpiresAt);
        Assert.NotNull(running.Profile.Live);
        Assert.Null(running.Profile.Problem);
        Assert.EndsWith("profile.yaml", running.Profile.Source, StringComparison.Ordinal);
        Assert.Equal(("heartbeat", "5s"), (running.Services[0].Name, running.Services[0].Every));
        Assert.Equal("healthy", running.Health?.Word);
    }

    [Fact]
    public void The_code_index_status_golden_carries_each_repos_health()
    {
        var status = Read<CodeIndexStatusView>("code-index-status", "all-repos");

        var health = status.Health ?? [];
        Assert.NotEmpty(health);
        Assert.Equal(status.Repos.Select(r => r.Repo), health.Select(h => h.Repo), StringComparer.Ordinal);
        Assert.All(health, h => Assert.Equal(("ok", null, null), (h.State, h.EmbeddingsPercent, h.Waiting)));
    }

    [Fact]
    public void The_ping_and_event_goldens_carry_the_fields_the_follower_reads()
    {
        var ping = Read<PingView>("ping", "running");
        var started = Read<EventView>("events", "process-started");

        Assert.Equal((4242, 1L), (ping.Pid, ping.Epoch));
        Assert.Equal((1L, "process.started"), (started.Sequence, started.Type));
    }

    [Fact]
    public void The_reviews_fix_golden_carries_the_file_error_the_fix_sheet_shows()
    {
        var sent = Read<ReviewFixSent>("reviews-fix", "file-not-written");

        Assert.Equal(("waiting", null), (sent.Went, sent.Session));
        Assert.StartsWith("The fix-task file wasn't written", sent.FileError, StringComparison.Ordinal);
    }

    // FakeSindriHandler writes the 2xx bodies by hand so it can serve any model state; it must write Sindri's shape.
    [Fact]
    public void The_fake_handler_writes_the_goldens_status_ping_and_event_shapes()
    {
        foreach (var scenario in (string[])["running-unapproved"])
        {
            var golden = GoldenFiles.Api("status", scenario);
            Assert.True(JsonNode.DeepEquals(JsonNode.Parse(golden), JsonNode.Parse(FakeSindriHandler.Status(Read<StatusView>("status", scenario)))), scenario);
        }

        Assert.True(JsonNode.DeepEquals(JsonNode.Parse(GoldenFiles.Api("ping", "running")), JsonNode.Parse(FakeSindriHandler.Ping(Read<PingView>("ping", "running")))));
        var goldenEvent = JsonNode.Parse(GoldenFiles.Api("events", "process-started"))!.AsObject(); // why: the golden is a JSON object
        var fakeEvent = JsonNode.Parse(FakeSindriHandler.Event(Read<EventView>("events", "process-started")))!.AsObject(); // why: as above
        Assert.Equal(goldenEvent.Select(p => p.Key), fakeEvent.Select(p => p.Key), StringComparer.Ordinal);
    }

    // F13 (test-1): the four spec-shaped status goldens (P2's, until P2 writes its own) say what the pill and the follower read.
    [Theory]
    [InlineData("running-healthy", "healthy", 41L, 41L, false, false, null, null, 0)]
    [InlineData("running-degraded", "degraded", 41L, 41L, false, false, null, null, 1)]
    [InlineData("running-rebuilding", "rebuilding", 12L, 42L, true, false, null, null, 0)]
    [InlineData("running-stalled", "stalled", 41L, 42L, false, true, 42L, "service.ran", 1)]
    public void The_spec_shaped_status_goldens_are_read_in_full(string scenario, string word, long cursor, long head, bool rebuilding, bool stalled, long? stalledAt, string? stalledType, int problems)
    {
        var status = AwJson.Deserialize<StatusView>(Encoding.UTF8.GetBytes(GoldenFiles.SpecShaped("status", scenario)), SindriApiClient.Json).Value;

        Assert.Equal(1, status.ApiVersion);
        Assert.Equal((word, problems), (status.Health!.Word, status.Health.Problems.Count));
        Assert.Equal((cursor, head, rebuilding, stalled, stalledAt, stalledType), (status.Views!.Cursor, status.Views.LedgerHead, status.Views.Rebuilding, status.Views.Stalled, status.Views.StalledAt, status.Views.StalledType));
        _ = Assert.NotNull(status.Views.ProjectedAt);
    }

    // F13: the handler writes the spec-shaped status goldens exactly, stalledAt and stalledType included.
    [Theory]
    [InlineData("running-healthy")]
    [InlineData("running-degraded")]
    [InlineData("running-rebuilding")]
    [InlineData("running-stalled")]
    public void The_fake_handler_writes_the_spec_shaped_status_goldens(string scenario)
    {
        var golden = GoldenFiles.SpecShaped("status", scenario);
        var status = AwJson.Deserialize<StatusView>(Encoding.UTF8.GetBytes(golden), SindriApiClient.Json).Value;

        Assert.True(JsonNode.DeepEquals(JsonNode.Parse(golden), JsonNode.Parse(FakeSindriHandler.Status(status))), scenario);
    }

    // A stale token: Sindri's human-stale-token golden as served, the token read again and the call retried once.
    [Fact]
    public async Task The_stale_token_golden_is_retried_once_with_the_token_read_again()
    {
        var handler = new FakeSindriHandler(new FakeSindriApi()) { RequiredToken = "fresh-start-token" };
        var token = new InMemoryHumanToken();
        token.Then("stale-start-token", "fresh-start-token");

        var status = await new SindriApiClient(new HttpClient(handler), token, TimeProvider.System).Status(_api, Token);

        Assert.True(status.IsSuccess);
        Assert.Equal(2, handler.Requests.Count);
    }

    // Refused after the retry: the golden's code and its detail as served (the sentence is the golden's, never a copy:
    // SND-AUTH-001's text changes in API P2 unit F), with no third call.
    [Theory]
    [InlineData("stale-start-token", "human-stale-token")]
    [InlineData(null, "human-no-token")]
    public async Task A_refusal_golden_after_the_retry_is_its_code_and_detail(string? held, string scenario)
    {
        var handler = new FakeSindriHandler(new FakeSindriApi()) { RequiredToken = "fresh-start-token" };
        var golden = Read<ProblemView>("status", scenario);

        var status = await new SindriApiClient(new HttpClient(handler), new InMemoryHumanToken(held), TimeProvider.System).Status(_api, Token);

        Assert.Equal(("SND-AUTH-001", ErrorKind.Forbidden, golden.Detail), (status.Error.Code, status.Error.Kind, status.Error.Message));
        Assert.Equal(["/sindri/status", "/sindri/status"], handler.Requests.Select(r => r.Path), StringComparer.Ordinal);
    }

    [Fact]
    public void The_p1_catalog_carries_every_field_the_control_renderer_reads()
    {
        var catalog = AwJson.Deserialize<CommandList>(Encoding.UTF8.GetBytes(GoldenFiles.Api("commands", "catalog")), SindriApiClient.Json).Value;

        var rebuild = Assert.Single(catalog.Rows, r => r.Action == "RebuildViews");
        Assert.Equal(("desk", "ready", "sindri views rebuild", false, false), (rebuild.Class, rebuild.Ui, rebuild.Cli, rebuild.Destructive, rebuild.ReadsViews));
        var stop = Assert.Single(catalog.Rows, r => r.Action == "StopProcess");
        Assert.Equal(("ready", true), (stop.Ui, stop.Destructive));
    }

    [Fact]
    public void The_relink_failed_pins_golden_reads_every_field_of_moved_and_a_pin_without_it_has_none()
    {
        var pins = Read<PinsView>("context-pins", "moved-relink-failed");

        var moved = Assert.IsType<PinMovedView>(pins.Pins[0].Moved);
        Assert.Equal(("v0", "v1", 2, 3, null, true), (moved.FromVersion, moved.ToVersion, moved.Gained, moved.Lost, moved.RecordsLinked, moved.RelinkFailed));
        Assert.Equal([("auth", "Sign in", 2), (null, null, 1)], [.. moved.To.Select(t => (t.Id, t.Name, t.Features))]);
        Assert.Null(pins.Pins[1].Moved);
        Assert.All(Read<PinsView>("context-pins", "approved").Pins, p => Assert.Null(p.Moved));
    }

    // A refusal's ProblemDetails names its SND code in title; any other body has none.
    private static T Read<T>(string slug, string scenario)
        where T : class =>
        AwJson.Deserialize<T>(Encoding.UTF8.GetBytes(GoldenFiles.Api(slug, scenario)), SindriApiClient.Json).Value;

    // A view's own "title" (an instance's, a child's) is no refusal: ProblemDetails also carries a numeric status.
    private static ProblemView? Problem(byte[] body)
    {
        if (JsonNode.Parse(body) is not JsonObject { } json || json["status"] is not JsonValue { } || json["traceId"] is null)
        {
            return null;
        }

        var problem = AwJson.Deserialize<ProblemView>(body, SindriApiClient.Json);
        return problem is { IsSuccess: true, Value.Title.Length: > 0 } ? problem.Value : null;
    }

    /// <summary>An event golden: one event (S1's), or the data of each frame a stream scenario sent, as an array (API P5).</summary>
    // One slug for GET and POST /requests (U3-API-5): the list-* scenarios are RequestList, the others RequestFiled.
    private static Result<object> AsRequests(byte[] body) =>
        JsonNode.Parse(body) is JsonObject node && node.ContainsKey("requests") ? As<RequestList>(body) : As<RequestFiledView>(body);

    // One slug for GET and POST /context/map/review: the post-* scenarios are the decision's reply, the others the queue.
    private static Result<object> AsReview(byte[] body) =>
        JsonNode.Parse(body) is JsonObject node && node.ContainsKey("result") ? As<ReviewDecisionResponse>(body) : As<ReviewQueueView>(body);

    private static Result<object> AsEvents(byte[] body)
    {
        var node = JsonNode.Parse(body);
        if (node is not JsonArray frames)
        {
            return As<EventView>(body);
        }

        foreach (var frame in frames)
        {
            var read = As<EventView>(Encoding.UTF8.GetBytes(frame?.ToJsonString() ?? "null"));
            if (read.IsError)
            {
                return read;
            }
        }

        return frames;
    }

    // GET /todos/{id}'s shape: { asOf, todo }; the todo is read as the app reads one from the list.
    private static Result<object> AsShownTodo(byte[] body)
    {
        var todo = (JsonNode.Parse(body) as JsonObject)?["todo"];
        return todo is null ? new Error("TEST-001", ErrorKind.Invalid, "no todo in the answer") : As<TodoView>(Encoding.UTF8.GetBytes(todo.ToJsonString()));
    }

    private static Result<object> AsJsonObject(byte[] body) =>
        JsonNode.Parse(body) is JsonObject node ? node : new Error("TEST-001", ErrorKind.Invalid, "the answer is not a JSON object");

    private static Result<object> As<T>(byte[] body)
        where T : class
    {
        var value = AwJson.Deserialize<T>(body, SindriApiClient.Json);
        return value.IsSuccess ? value.Value : value.Error;
    }
}
