namespace Aw.Sindri.Ui.ViewModels.Shell;

/// <summary>
/// The app's own copy (<see cref="Labels"/>) as the views bind it: compiled bindings resolve public members only, and the
/// labels stay internal, so each view model hands its view this instance as its Text.
/// </summary>
internal sealed class ShellText
{
    /// <summary>The one instance.</summary>
    internal static ShellText Instance { get; } = new();

    /// <summary>U2's copy: the queue and the item sheet.</summary>
    public Item.ItemText Items { get; } = Item.ItemText.Instance;

    /// <summary><see cref="Labels.ActionsCard"/>.</summary>
    public string ActionsCard { get; } = Labels.ActionsCard;

    /// <summary><see cref="Labels.Back"/>.</summary>
    public string Back { get; } = Labels.Back;

    /// <summary><see cref="Labels.CheckIt"/>.</summary>
    public string CheckIt { get; } = Labels.CheckIt;

    /// <summary><see cref="Labels.CloseSheet"/>.</summary>
    public string CloseSheet { get; } = Labels.CloseSheet;

    /// <summary><see cref="Labels.CloseSheetName"/>.</summary>
    public string CloseSheetName { get; } = Labels.CloseSheetName;

    /// <summary><see cref="Labels.Connecting"/>.</summary>
    public string Connecting { get; } = Labels.Connecting;

    /// <summary><see cref="Labels.ControlPlaneCard"/>.</summary>
    public string ControlPlaneCard { get; } = Labels.ControlPlaneCard;

    /// <summary><see cref="Labels.DoctorFix"/>.</summary>
    public string DoctorFix { get; } = Labels.DoctorFix;

    /// <summary><see cref="Labels.JobsLink"/>.</summary>
    public string JobsLink { get; } = Labels.JobsLink;

    /// <summary><see cref="Labels.JobsLinkName"/>.</summary>
    public string JobsLinkName { get; } = Labels.JobsLinkName;

    /// <summary><see cref="Labels.GoKey"/>.</summary>
    public string GoKey { get; } = Labels.GoKey;

    /// <summary><see cref="Labels.HealthKey"/>.</summary>
    public string HealthKey { get; } = Labels.HealthKey;

    /// <summary><see cref="Labels.Health"/>.</summary>
    public string Health { get; } = Labels.Health;

    /// <summary><see cref="Labels.HealthPanel"/>.</summary>
    public string HealthPanel { get; } = Labels.HealthPanel;

    /// <summary><see cref="Labels.Workflows"/>.</summary>
    public string Workflows { get; } = Labels.Workflows;

    /// <summary><see cref="Labels.Work"/>.</summary>
    public string Work { get; } = Labels.Work;

    /// <summary><see cref="Labels.Recipes"/>.</summary>
    public string Recipes { get; } = Labels.Recipes;

    /// <summary><see cref="Labels.Context"/>.</summary>
    public string Context { get; } = Labels.Context;

    /// <summary><see cref="Labels.Agents"/>.</summary>
    public string Agents { get; } = Labels.Agents;

    /// <summary><see cref="Labels.System"/>.</summary>
    public string System { get; } = Labels.System;

    /// <summary>What the Work page waits for (<see cref="Labels.ArrivesWith"/>).</summary>
    public string ArrivesWork { get; } = Labels.ArrivesWork;

    /// <summary>What the Recipes page waits for (<see cref="Labels.ArrivesWith"/>).</summary>
    public string ArrivesRecipes { get; } = Labels.ArrivesRecipes;

    /// <summary>What the Context page waits for (<see cref="Labels.ArrivesWith"/>).</summary>
    public string ArrivesContext { get; } = Labels.ArrivesContext;

    /// <summary><see cref="Labels.Home"/>.</summary>
    public string Home { get; } = Labels.Home;

    /// <summary><see cref="Labels.LastEventCard"/>.</summary>
    public string LastEventCard { get; } = Labels.LastEventCard;

    /// <summary><see cref="Labels.LeaseCard"/>.</summary>
    public string LeaseCard { get; } = Labels.LeaseCard;

    /// <summary><see cref="Labels.LooksAgain"/>.</summary>
    public string LooksAgain { get; } = Labels.LooksAgain;

    /// <summary><see cref="Labels.MarkAllSeen"/>.</summary>
    public string MarkAllSeen { get; } = Labels.MarkAllSeen;

    /// <summary><see cref="Labels.Undo"/>.</summary>
    public string Undo { get; } = Labels.Undo;

    /// <summary><see cref="Labels.UndoMarkAllSeen"/>.</summary>
    public string UndoMarkAllSeen { get; } = Labels.UndoMarkAllSeen;

    /// <summary><see cref="Labels.NeedsYouPanel"/>.</summary>
    public string NeedsYouPanel { get; } = Labels.NeedsYouPanel;

    /// <summary><see cref="Labels.NoServices"/>.</summary>
    public string NoServices { get; } = Labels.NoServices;

    /// <summary><see cref="Labels.NotRunning"/>.</summary>
    public string NotRunning { get; } = Labels.NotRunning;

    /// <summary><see cref="Labels.NotResponding"/>.</summary>
    public string NotResponding { get; } = Labels.NotResponding;

    /// <summary><see cref="Labels.NotRespondingLead"/>.</summary>
    public string NotRespondingLead { get; } = Labels.NotRespondingLead;

    /// <summary><see cref="Labels.NotServedYet"/>.</summary>
    public string NotServedYet { get; } = Labels.NotServedYet;

    /// <summary><see cref="Labels.Pages"/>.</summary>
    public string Pages { get; } = Labels.Pages;

    /// <summary><see cref="Labels.ProfileCard"/>.</summary>
    public string ProfileCard { get; } = Labels.ProfileCard;

    /// <summary><see cref="Labels.QueuedPanel"/>.</summary>
    public string QueuedPanel { get; } = Labels.QueuedPanel;

    /// <summary><see cref="Labels.Refresh"/>.</summary>
    public string Refresh { get; } = Labels.Refresh;

    /// <summary><see cref="Labels.RunningPanel"/>.</summary>
    public string RunningPanel { get; } = Labels.RunningPanel;

    /// <summary><see cref="Labels.Search"/>.</summary>
    public string Search { get; } = Labels.Search;

    /// <summary><see cref="Labels.SearchKey"/>.</summary>
    public string SearchKey { get; } = Labels.SearchKey;

    /// <summary><see cref="Labels.SearchKeyLetter"/>.</summary>
    public string SearchKeyLetter { get; } = Labels.SearchKeyLetter;

    /// <summary><see cref="Labels.StartWorkflow"/>.</summary>
    public string StartWorkflow { get; } = Labels.StartWorkflow;

    /// <summary><see cref="Labels.Stale"/>.</summary>
    public string Stale { get; } = Labels.Stale;

    /// <summary><see cref="Labels.Sending"/>.</summary>
    public string Sending { get; } = Labels.Sending;

    /// <summary><see cref="Labels.Services"/>.</summary>
    public string Services { get; } = Labels.Services;

    /// <summary><see cref="Labels.ServicesCard"/>.</summary>
    public string ServicesCard { get; } = Labels.ServicesCard;

    /// <summary><see cref="Labels.Settings"/>.</summary>
    public string Settings { get; } = Labels.Settings;

    /// <summary><see cref="Labels.Sheet"/>.</summary>
    public string Sheet { get; } = Labels.Sheet;

    /// <summary><see cref="Labels.Sindri"/>.</summary>
    public string Sindri { get; } = Labels.Sindri;

    /// <summary><see cref="Labels.StartFix"/>.</summary>
    public string StartFix { get; } = Labels.StartFix;

    /// <summary><see cref="Labels.StartIt"/>.</summary>
    public string StartIt { get; } = Labels.StartIt;

    /// <summary><see cref="Labels.Unreadable"/>.</summary>
    public string Unreadable { get; } = Labels.Unreadable;

    /// <summary><see cref="Labels.VersionMismatch"/>.</summary>
    public string VersionMismatch { get; } = Labels.VersionMismatch;

    /// <summary><see cref="Labels.VersionMismatchFix"/>.</summary>
    public string VersionMismatchFix { get; } = Labels.VersionMismatchFix;

    /// <summary><see cref="Labels.VersionMismatchFixLead"/>.</summary>
    public string VersionMismatchFixLead { get; } = Labels.VersionMismatchFixLead;
}
