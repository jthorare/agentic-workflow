using System.Globalization;
using System.Text;
using Aw.Sindri.Features.ContextCalls;

namespace Aw.Sindri.Host.Text;

/// <summary>The text of a finished map build job: the draft line and one 'degraded:' line for each thing the build did without, reason included ('clusters: timed out after 60 min').</summary>
internal static class MapBuildJobText
{
    internal static string Render(BuildContextMapResult result)
    {
        ArgumentNullException.ThrowIfNull(result);
        var text = new StringBuilder(string.Create(CultureInfo.InvariantCulture, $"draft {result.Draft} for {result.Repo} · {result.Proposed} proposed\n"));
        foreach (var note in result.Degraded)
        {
            _ = text.Append("degraded: ").AppendLine(note);
        }

        foreach (var note in result.Notes)
        {
            _ = text.Append("note: ").AppendLine(note);
        }

        if (result.Replaced is { } replaced && (replaced.Features | replaced.Links | replaced.Undone | replaced.Carried) != 0)
        {
            _ = text.AppendLine(string.Create(CultureInfo.InvariantCulture, $"migration: {replaced.Features:N0} model features and {replaced.Links:N0} model links replaced; {replaced.Undone:N0} decisions had no target and were undone; {replaced.Carried:N0} carried over"));
        }

        if (result.Checks is { } checks)
        {
            _ = text.AppendLine(checks);
        }

        return text.ToString();
    }
}
