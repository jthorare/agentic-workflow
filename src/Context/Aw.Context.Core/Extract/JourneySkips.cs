namespace Aw.Context.Core.Extract;

/// <summary>Journey steps and files the flow pass left out, by reason.</summary>
/// <param name="UnsupportedForm">Steps in a form the reader does not follow (a CSS locator, a dynamic id, a plan step of another shape) and files that did not parse.</param>
/// <param name="NoMatch">Steps that matched no surface or action.</param>
/// <param name="Denied">Journey files under a denied path (never read).</param>
/// <param name="OverSizeCap">Journey files over the extractor's size cap.</param>
/// <param name="OverFileCap">Journey files past <see cref="Journeys.MaxFiles"/>.</param>
public sealed record JourneySkips(int UnsupportedForm, int NoMatch, int Denied, int OverSizeCap, int OverFileCap)
{
    /// <summary>Nothing skipped.</summary>
    public static JourneySkips None { get; } = new(0, 0, 0, 0, 0);
}
