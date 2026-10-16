using System.Globalization;
using System.Text;
using Aw.Sindri.Core.Workflow.Registry;

namespace Aw.Sindri.Host.Commands.Recipe;

/// <summary>The 'recipe approve' and 'recipe revoke' previews and results as text (engine spec §4.2, §6.3). Pure: RecipeDeskCommands writes it.</summary>
internal static class RecipePreviewText
{
    /// <summary>The approval preview: the recipe, its diff, capability uses, effects, pins and evals, and for a re-approval what changed.</summary>
    /// <param name="p">The preview.</param>
    /// <param name="evals">The CLI's own eval line when it read the file (which eval files are present), else null for the server's.</param>
    /// <returns>The text.</returns>
    internal static string Approve(ApprovalPreview p, string? evals)
    {
        var text = new StringBuilder()
            .Append(CultureInfo.InvariantCulture, $"recipe   {p.Id.Value} version {p.Version}\n")
            .Append(CultureInfo.InvariantCulture, $"hash     {p.Hash.Value}\n")
            .Append(CultureInfo.InvariantCulture, $"base     {(p.Base is { } b ? $"{b.Id.Value}@{b.Hash.Display}" : "none")}\n")
            .Append("diff against the last approved version: ").Append(p.Diff is null ? "nothing\n" : "\n" + Indent(p.Diff));
        _ = Section(text, "capability uses", p.CapabilityUses.Select(static u =>
            $"{u.Block.Value} on {u.From} -> {u.To}: {u.Capabilities}{(u.Parameters.Count == 0 ? string.Empty : " (" + string.Join(", ", u.Parameters) + ")")}"));
        _ = Section(text, "effects", p.Effects.Select(static e => $"{e.Kind.Value} ({(e.Allowlisted ? "on" : "not on")} the effect allowlist)"));
        _ = Section(text, "pinned blocks", p.Pins.Blocks.Select(static b => $"{b.Block.Value} {b.Hash.Value}"));
        _ = Section(text, "pinned children", p.Pins.Children.Select(static c => $"{c.Recipe.Value}@{c.Hash.Display}"));
        _ = text.Append("evals    ").Append(evals ?? p.Evals ?? "none declared").Append('\n');
        if (p.IsDrift || p.Resumes.Count > 0)
        {
            _ = Section(text, "changed blocks (drift)", p.Changed.Select(static c => $"{c.Block.Value} {c.Approved.Display()} -> {c.Build.Display()}"));
            _ = Section(text, "open instances woken (those parked on this drift resume)", p.Resumes.Select(static i => i.Value));
        }

        return text.ToString();
    }

    /// <summary>The revoke preview: the instances that park, the takeovers it ends, the cascade and the parents it marks child-stale.</summary>
    /// <param name="p">The preview.</param>
    /// <returns>The text.</returns>
    internal static string Revoke(RevokePreview p)
    {
        var text = new StringBuilder()
            .Append(CultureInfo.InvariantCulture, $"recipe   {p.Id.Value} version {p.Version}\n")
            .Append(CultureInfo.InvariantCulture, $"hash     {p.Hash.Value}\n");
        _ = Section(text, "instances that will park", p.Parks.Select(static i => i.Value));
        _ = Section(text, "takeovers it ends", p.TakeoversEnded.Select(static i => i.Value));
        _ = Section(text, "generated recipes revoked with it", p.Cascade.Select(static r => $"{r.Id.Value}@{r.Hash.Display}"));
        _ = Section(text, "parents pinning it as a child (marked child-stale)", p.Parents.Select(static r => $"{r.Id.Value}@{r.Hash.Display}"));
        return text.ToString();
    }

    private static StringBuilder Section(StringBuilder text, string title, IEnumerable<string> lines)
    {
        var all = lines.ToList();
        _ = text.Append(title).Append(':');
        return all.Count == 0
            ? text.Append(" none\n")
            : text.Append('\n').Append(string.Concat(all.Select(static l => "  " + l + "\n")));
    }

    private static string Indent(string diff) => string.Concat(diff.Split('\n').Where(static l => l.Length > 0).Select(static l => "  " + l + "\n"));

    private static string Display(this Core.Workflow.Ids.DescriptorHash hash) => hash.Value["sha256:".Length..][..12];
}
