using System.Globalization;
using Aw.Sindri.Ui.Navigation;

namespace Aw.Sindri.Ui.ViewModels;

/// <summary>The app's own copy (<see cref="Labels"/>, continued): the tray, Settings, and page names.</summary>
internal static partial class Labels
{
    // The tray (§5). U0 knows no counts; U2/U3 add "Sindri · N running · nothing needs you" and the needs-you tooltips.

    /// <summary>The tray's Neutral tooltip in U0.</summary>
    internal const string TrayRunning = "Sindri · running";

    /// <summary>The tray's Stale tooltip.</summary>
    internal const string TrayNotResponding = "Sindri not responding";

    /// <summary>The tray's Not running tooltip.</summary>
    internal const string TrayNotRunning = "Sindri isn't running · run sindri";

    /// <summary>The tray menu's open item.</summary>
    internal const string TrayOpen = "Open Sindri";

    /// <summary>The tray menu's quit item.</summary>
    internal const string TrayQuit = "Quit";

    // Settings (§3.3 ⌘,, §11.4, §14.2).

    /// <summary>The in-app switch that forces reduced motion.</summary>
    internal const string ReduceMotion = "Reduce motion";

    /// <summary>The note beside the switch while the OS reduces motion (the switch can't turn the OS setting off).</summary>
    internal const string SystemReducesMotion = "On in your system settings";

    /// <summary>The text size slider.</summary>
    internal const string TextSize = "Text size";

    /// <summary>The noun a sheet's name starts with for a link kind, or empty when the subject names itself (an item, decision, job or row id).</summary>
    /// <param name="kind">The link's kind.</param>
    /// <returns>The noun, capitalised ("Workflow"), or empty.</returns>
    internal static string KindNoun(DeepLinkKind kind) => kind switch
    {
        DeepLinkKind.Workflow => "Workflow",
        DeepLinkKind.Recipe => "Recipe",
        DeepLinkKind.Node => "Node",
        DeepLinkKind.Session => "Session",
        DeepLinkKind.System => "System",
        DeepLinkKind.Service => "Service",
        DeepLinkKind.Job => "Job",
        DeepLinkKind.HomeNeedsYou or DeepLinkKind.NeedsYou or DeepLinkKind.Item or DeepLinkKind.Decision or DeepLinkKind.Setup => string.Empty,
        _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, message: null),
    };

    /// <summary>A page's name, as the top bar and the page region's accessible name say it (§11.2).</summary>
    /// <param name="page">The page.</param>
    /// <returns>The words.</returns>
    internal static string Page(PageId page) => page switch
    {
        PageId.Home => Home,
        PageId.Workflows => Workflows,
        PageId.Work => Work,
        PageId.Recipes => Recipes,
        PageId.Context => Context,
        PageId.Agents => Agents,
        PageId.System => System,
        PageId.Jobs => JobsPage,
        PageId.Settings => Settings,
        _ => throw new ArgumentOutOfRangeException(nameof(page), page, message: null),
    };

    /// <summary>Work's placeholder.</summary>
    internal const string ArrivesWork = "arrives with U3' and the workflow routes (API P11)";

    /// <summary>Context's placeholder.</summary>
    internal const string ArrivesContext = "arrives with U9 and the context lens routes (CTX phase 4)";

    /// <summary>What an unbuilt page says it waits for ("arrives with U8 and the workflow routes (API P11)"), or null for a built page.</summary>
    /// <param name="page">The page.</param>
    /// <returns>The words, or null.</returns>
    internal static string? ArrivesWith(PageId page) => page switch
    {
        PageId.Work => ArrivesWork,
        PageId.Context => ArrivesContext,
        PageId.Home or PageId.Workflows or PageId.Recipes or PageId.Agents or PageId.System or PageId.Jobs or PageId.Settings => null,
        _ => throw new ArgumentOutOfRangeException(nameof(page), page, message: null),
    };

    /// <summary>A scale as a percentage ("110%").</summary>
    /// <param name="scale">The scale (1.0 is 100%).</param>
    /// <returns>The words.</returns>
    internal static string Percent(double scale) => string.Create(CultureInfo.InvariantCulture, $"{Math.Round(scale * 100, MidpointRounding.AwayFromZero):0}%");

    /// <summary>The System page's disclosure heading: the control plane, services, profile, commands and last event.</summary>
    internal const string SystemDetails = "Details";

    /// <summary>The System page's sentence while everything is running.</summary>
    internal const string AllRunning = "Everything is running.";

    /// <summary>The sentence while the status carries no health data yet.</summary>
    internal const string CheckingHealth = "Checking…";

    /// <summary>The sentence while the profile waits for the person's approval.</summary>
    internal const string ProfileNeedsApproval = "The profile needs your approval.";

    /// <summary>The sentence when Sindri runs but the status names no problem in words.</summary>
    internal const string RunningNeedsALook = "Sindri is running, but something needs a look.";

    /// <summary>The sentence when Sindri has stopped answering.</summary>
    internal const string StoppedAnswering = "Sindri has stopped answering, so what you see may be out of date.";

    /// <summary>The sentence when Sindri isn't running.</summary>
    internal const string SindriNotRunning = "Sindri isn't running.";

    /// <summary>The sentence when the app can't read Sindri's answer.</summary>
    internal const string CantReadAnswer = "Sindri is running, but this app can't read its answer.";

    /// <summary>The sentence when Sindri and the app speak different versions.</summary>
    internal const string DifferentVersions = "Sindri and this app are different versions.";

    /// <summary>The sentence naming what needs a look.</summary>
    /// <param name="problems">What the status names as wrong, in words.</param>
    /// <returns>The sentence.</returns>
    internal static string NeedsALook(string problems) => "Something needs a look: " + problems + ".";
}
