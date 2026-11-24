using System.Globalization;

namespace Aw.Sindri.Ui.ViewModels.Projects;

/// <summary>One glyph of a row's PRs cell: a PR state and how many open PRs are in it. The shape tells the state, so colour is never the only cue.</summary>
internal sealed class ProjectPrGlyph
{
    internal ProjectPrGlyph(string state, int count, bool afterHairline)
    {
        (State, Count, AfterHairline) = (state, count, afterHairline);
    }

    /// <summary>The served state: "ciRed", "waiting", "unknown", "ready" or "draft".</summary>
    public string State { get; }

    /// <summary>How many open PRs are in this state.</summary>
    public int Count { get; }

    /// <summary>The count as drawn.</summary>
    public string CountText => Count.ToString(CultureInfo.InvariantCulture);

    /// <summary>True for the drafts' glyph when other glyphs come before it: a hairline sets it apart, since drafts never count toward health.</summary>
    public bool AfterHairline { get; }

    /// <summary>True for red CI.</summary>
    public bool IsCiRed => Is("ciRed");

    /// <summary>True for a PR that waits on a reviewer.</summary>
    public bool IsWaiting => Is("waiting");

    /// <summary>True for a PR whose CI is not checked yet.</summary>
    public bool IsUnknown => Is("unknown");

    /// <summary>True for a PR that is ready.</summary>
    public bool IsReady => Is("ready");

    /// <summary>True for a draft.</summary>
    public bool IsDraft => Is("draft");

    /// <summary>The glyph's words for a screen reader: "2 CI red".</summary>
    public string Name => Labels.ProjectPrGlyphName(State, Count);

    private bool Is(string state) => string.Equals(State, state, StringComparison.Ordinal);
}
