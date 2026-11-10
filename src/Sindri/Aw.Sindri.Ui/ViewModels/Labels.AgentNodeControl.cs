using Aw.Kernel;

namespace Aw.Sindri.Ui.ViewModels;

/// <summary>The app's own copy (<see cref="Labels"/>, continued): Take over and Hand back on a run's agent node (#2472; the item take over is in Labels.Takeover.cs). Never a served sentence.</summary>
internal static partial class Labels
{
    /// <summary>The button on a waiting agent node that takes its agent over.</summary>
    internal const string WfTakeOver = "Take over";

    /// <summary>The same button while its call is on the way.</summary>
    internal const string WfTakeOverBusy = "Taking over…";

    /// <summary>The button on a taken-over agent node that gives it back.</summary>
    internal const string WfHandBack = "Hand back";

    /// <summary>The same button while its call is on the way.</summary>
    internal const string WfHandBackBusy = "Handing back…";

    /// <summary>The note after a take over was accepted.</summary>
    internal const string WfTakeOverDone = "You have the agent. The workflow waits until you hand it back.";

    /// <summary>The note after a hand back was accepted.</summary>
    internal const string WfHandBackDone = "Handed back. The agent carries on.";

    /// <summary>The note when a take over was refused or failed: the reason follows.</summary>
    /// <param name="error">The failure.</param>
    /// <returns>The words.</returns>
    internal static string WfTakeOverRefused(Error error) => "Couldn't take over the agent. " + InstanceRefused(error);

    /// <summary>The note when a hand back was refused or failed: the reason follows.</summary>
    /// <param name="error">The failure.</param>
    /// <returns>The words.</returns>
    internal static string WfHandBackRefused(Error error) => "Couldn't hand the agent back. " + InstanceRefused(error);
}
