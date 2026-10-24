using System.Globalization;
using Aw.Sindri.Ui.Api.Workflow;
using Aw.Sindri.Ui.ViewModels.NeedsYou;

namespace Aw.Sindri.Ui.Testing;

/// <summary>Builders for needs-you rows in the shapes the screens fixture gives (a park, a decision, a failure, a profile drift).</summary>
internal static class NeedsYouRows
{
    internal const string ProfileHash = "932d876195e0684a7eef2cfb155d03297bc90219dd98257fb87549114040c79a";

    internal const string Command = "sindri workflow answer ITM-01JC8 --grant 1";

    internal const string DeskInstance = "WFI-44393da815f188cea9c6fe30ae2a30a6";

    /// <summary>An open desk task (a person's approval): the engine's CompleteStep, ready, and the instance it reports on.</summary>
    internal static NeedsYouEntry Desk(string instance = DeskInstance) => new("desk:" + instance, NeedsYouUrgency.Colour)
    {
        Kind = "park",
        Chip = new StateChip("DESK TASK", ChipTone.Attn),
        ShownId = "ITM-01JL1",
        Title = "Try the engine",
        Repo = "demo",
        Label = "desk task",
        Reason = "a desk task is open (ITM-01JL1/dev-desk-round-trip/1)",
        Since = A.Noon.AddMinutes(-26),
        PrimaryAction = "CompleteStep",
        Actions = [new NeedsYouAction("CompleteStep", "Complete", ActionReadiness.Ready, "sindri workflow status ITM-01JL1/dev-desk-round-trip/1", RejectLike: false)],
        Subject = new NeedsYouSubject("instance", instance),
        ItemId = "ITM-01JL1",
        Cli = "sindri workflow status ITM-01JL1/dev-desk-round-trip/1",
    };

    /// <summary>A paused run whose rounds are used up: "Grant rounds" is protected, so the app shows its command.</summary>
    internal static NeedsYouEntry Grant(string id = "park:WFI-1") => Park(id) with
    {
        PrimaryAction = "ApplyCommand:answer-grant",
        Actions = [new NeedsYouAction("ApplyCommand:answer-grant", "Grant rounds", ActionReadiness.Ready, "sindri workflow answer ITM-01JC8/step/1 --grant 1", RejectLike: false)],
        Cli = "sindri workflow answer ITM-01JC8/step/1 --grant 1",
    };

    /// <summary>A parked item: a protected primary and its command.</summary>
    internal static NeedsYouEntry Park(string id = "park:ITM-01JC8", int minutes = 12) => new(id, NeedsYouUrgency.Pulse)
    {
        Kind = "park",
        Chip = new StateChip("PARKED", ChipTone.Attn),
        ShownId = "ITM-01JC8",
        Title = "Retry storm in outbox dispatcher",
        Repo = "agentic-workflow",
        Label = "checks failed twice with the same findings",
        Reason = "checks failed twice with the same findings",
        Since = A.Noon.AddMinutes(-minutes),
        PrimaryAction = "GrantRounds",
        Actions = [new NeedsYouAction("GrantRounds", "Grant rounds", ActionReadiness.Ready, Command, RejectLike: false)],
        Subject = new NeedsYouSubject("item", "ITM-01JC8"),
        ItemId = "ITM-01JC8",
        Cli = Command,
    };

    /// <summary>A decision: Accept (recommended, first) and Decline (rejectLike).</summary>
    internal static NeedsYouEntry Decision(string id = "decision:DEC-1", string? item = "ITM-01JD2") => new(id, NeedsYouUrgency.Pulse)
    {
        Kind = "decision",
        Chip = new StateChip("DECISION", ChipTone.Attn),
        ShownId = "REQ-01JD4",
        Title = "Re-scope: drop the iOS head from this item",
        Repo = "sindri-app",
        Label = "decision · re-scope",
        Reason = "re-scope request from session:cli",
        Since = A.Noon.AddMinutes(-26),
        PrimaryAction = "Decide",
        Actions =
        [
            new NeedsYouAction("Decide", "Accept", ActionReadiness.Ready, "sindri decide DEC-1 accept", RejectLike: false) { OptionId = "accept", Recommended = true },
            new NeedsYouAction("Decide", "Decline", ActionReadiness.Ready, "sindri decide DEC-1 decline", RejectLike: true) { OptionId = "decline" },
        ],
        Subject = new NeedsYouSubject("decision", "DEC-1"),
        ItemId = item,
        Cli = "sindri decide DEC-1 <choice>",
    };

    /// <summary>A failed service: RunService and PauseService are not available yet.</summary>
    internal static NeedsYouEntry Failure(string id = "failure:service:index") => new(id, NeedsYouUrgency.Colour)
    {
        Kind = "failure",
        Chip = new StateChip("FAILED", ChipTone.Fail),
        Title = "Nightly index build",
        Label = "failure · index",
        Reason = "SND-SCHED-001: A background service failed",
        Since = A.Noon.AddHours(-9),
        PrimaryAction = "RunService",
        Actions =
        [
            new NeedsYouAction("RunService", "Run again", ActionReadiness.Pending, "sindri services run index", RejectLike: false),
            new NeedsYouAction("PauseService", "Pause", ActionReadiness.Pending, "sindri services pause index", RejectLike: false),
        ],
        Subject = new NeedsYouSubject("service", "index"),
        Cli = "sindri services run index",
    };

    /// <summary>A drifted profile: calm, a protected approve, and the subject optional.</summary>
    internal static NeedsYouEntry Profile(NeedsYouSubject? subject = null) => new("profile:drift", NeedsYouUrgency.Calm)
    {
        Kind = "profile",
        Chip = new StateChip("PROFILE", ChipTone.Attn),
        Title = "Profile changed since approval · 2 keys",
        Label = "profile · drifted",
        Reason = "1 key path changed: services.scorer.every",
        Since = A.Noon.AddHours(-2),
        PrimaryAction = "ApproveProfile",
        Actions = [new NeedsYouAction("ApproveProfile", "Approve", ActionReadiness.Ready, "sindri profile approve 3f1c09", RejectLike: false)],
        Subject = subject,
        Cli = "sindri profile approve 3f1c09",
    };

    /// <summary>The drifted profile with its full hash as the subject and an approve the catalog lets the app confirm.</summary>
    internal static NeedsYouEntry ProfileReady(string hash = ProfileHash) => Profile(new NeedsYouSubject("profile", hash)) with
    {
        Actions = [new NeedsYouAction("ApproveProfile", "Approve", ActionReadiness.Ready, "sindri profile approve " + hash[..6], RejectLike: false)],
        Cli = "sindri profile approve " + hash[..6],
    };

    /// <summary>The setup row that waits on approve-all: its primary is ApproveAllRecipes.</summary>
    internal static NeedsYouEntry SetupRecipes(ActionReadiness readiness = ActionReadiness.Ready) => new("setup:recipes", NeedsYouUrgency.Calm)
    {
        Kind = "setup",
        Chip = new StateChip("SETUP", ChipTone.Attn),
        Title = "2 new workflows to approve",
        Label = "setup · workflows",
        Reason = "2 workflows are new or changed",
        Since = A.Noon.AddHours(-1),
        PrimaryAction = "ApproveAllRecipes",
        Actions = [new NeedsYouAction("ApproveAllRecipes", "Approve all", readiness, "sindri recipe approve-all", RejectLike: false)],
        Cli = "sindri recipe approve-all",
    };

    /// <summary>A changed workflow: its single approve is a later slice's, so the bar shows the command only.</summary>
    internal static NeedsYouEntry Recipe(string id = "workflow.step") => new("recipe:" + id, NeedsYouUrgency.Calm)
    {
        Kind = "recipe",
        Chip = new StateChip("RECIPE", ChipTone.Attn),
        Title = "One step changed",
        Label = "workflow · changed",
        Reason = "the recipe changed since it was approved",
        Since = A.Noon.AddHours(-1),
        PrimaryAction = "ApproveRecipe",
        Actions = [new NeedsYouAction("ApproveRecipe", "Approve", ActionReadiness.Ready, "sindri recipe approve " + id, RejectLike: false)],
        Subject = new NeedsYouSubject("recipe", id),
        Cli = "sindri recipe approve " + id,
    };

    /// <summary>A pulse row with just an id, for lists that need many.</summary>
    internal static NeedsYouEntry Many(int n)
    {
        var shown = string.Create(CultureInfo.InvariantCulture, $"ITM-{n:D5}");
        return Park("park:" + shown) with { ShownId = shown };
    }
}
