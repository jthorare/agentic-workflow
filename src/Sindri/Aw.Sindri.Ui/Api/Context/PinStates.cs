namespace Aw.Sindri.Ui.Api.Context;

/// <summary>The values of <see cref="PinView.State"/>.</summary>
#pragma warning disable MA0182 // Q7b: the pin cards read it in the next commits
internal static class PinStates
{
    /// <summary>The live entry is the approved one.</summary>
    internal const string Approved = "approved";

    /// <summary>The live entry is new or differs from the approved one: it waits for an approval.</summary>
    internal const string Waiting = "waiting";

    /// <summary>The approved profile has it and the live one doesn't: its removal waits for an approval.</summary>
    internal const string Removing = "removing";
}
#pragma warning restore MA0182
