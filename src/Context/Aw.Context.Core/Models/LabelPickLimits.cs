namespace Aw.Context.Core.Models;

/// <summary>
/// The numbers of the Jev pick (spec §6.3 to §6.5), in one place: R-0 measures them on a real map and changes only this type. The labeler, the Host port and the tests all read it.
/// </summary>
public static class LabelPickLimits
{
    /// <summary>The share of Jev's input cap everything in one call (every question text and option text) may take; the rest is the JSON around it and Jev's own instructions.</summary>
    public const double CapShare = 0.8;

    /// <summary>The most questions in one call, even when more would fit: one failed call loses at most this many labels.</summary>
    public const int MaxQuestions = 100;

    /// <summary>A choice with fewer options than this is not a question.</summary>
    public const int MinOptions = 2;

    /// <summary>The most options in one question.</summary>
    public const int MaxOptions = 5;

    /// <summary>The confidence under which an answer counts as undecided.</summary>
    public const double MinConfidence = 0.5;

    /// <summary>How many times a rejected pack is split in halves (a pack of 100 makes at most 1 + 2 + 4 calls).</summary>
    public const int MaxSplitDepth = 2;

    /// <summary>The time budget of one pick call.</summary>
    public static TimeSpan Budget { get; } = TimeSpan.FromSeconds(45);

    /// <summary>The characters one call may hold at an input cap.</summary>
    /// <param name="capChars">Jev's input cap in characters ('providers.inputCapChars').</param>
    /// <returns>The share of it a call may use.</returns>
    public static int CallChars(int capChars) => (int)(capChars * CapShare);
}
