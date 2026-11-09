using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using Aw.Evidence.Core.Files;
using Aw.Kernel;

namespace Aw.Evidence.Core.Settings;

/// <summary>
/// The stack slot a worktree claims (SKILL 'Worktree stacks'): the slot file in its root names slot N, and slot N's database is the
/// {slot}-templated name. Doctor checks that slot's stack and provenance queries that slot's database.
/// </summary>
/// <param name="Number">The slot, 1 or more.</param>
/// <param name="Database">The slot's database, or null when no database template is configured.</param>
/// <param name="PortStep">The port step per slot (UI_EVIDENCE_SLOT_PORT_STEP).</param>
public sealed partial record WorktreeSlot(int Number, string? Database, int PortStep)
{
    /// <summary>The slot's counterpart of a main-stack port: the port + step * N.</summary>
    /// <param name="mainPort">The main stack's port.</param>
    /// <returns>The slot's port.</returns>
    public int PortFor(int mainPort) => mainPort + (PortStep * Number);

    /// <summary>Reads the slot the worktree's slot file names.</summary>
    /// <param name="slots">The deployment's slot settings, or null when none are configured.</param>
    /// <param name="files">Reads the slot file.</param>
    /// <param name="worktree">The worktree root, with no trailing slash.</param>
    /// <param name="cancellationToken">Cancels the read.</param>
    /// <returns>The slot, or AW-EVIDENCE-001 whose message says why the worktree has no usable slot.</returns>
    public static async ValueTask<Result<WorktreeSlot>> ReadAsync(SlotSettings? slots, IRunFiles files, string worktree, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(files);
        if (slots is null)
        {
            return EvidenceErrors.Invalid.With($"{worktree} was named but no worktree slots are configured (set UI_EVIDENCE_SLOT_FILE; see SKILL.md \"Worktree stacks\")");
        }

        var path = $"{worktree}/{slots.FileName}";
        var read = await files.ReadAsync(path, cancellationToken).ConfigureAwait(false);
        if (read.IsError)
        {
            return EvidenceErrors.Invalid.With(read.Error.Code == EvidenceErrors.FileNotFound.Code
                ? $"{path} does not exist: claim a stack slot there first"
                : $"{path} can't be read: {read.Error.Message}");
        }

        if (SlotNumber(Encoding.UTF8.GetString(read.Value), slots.Key) is not { } number || number > slots.Count)
        {
            return EvidenceErrors.Invalid.With($"{path} has no {slots.Key} between 1 and {slots.Count}");
        }

        if (slots.Database is not { } template)
        {
            return new WorktreeSlot(number, Database: null, slots.PortStep);
        }

        var database = template.Replace("{slot}", number.ToString(CultureInfo.InvariantCulture), StringComparison.Ordinal);
        return DatabaseName.IsMatch(database)
            ? new WorktreeSlot(number, database, slots.PortStep)
            : EvidenceErrors.Invalid.With("UI_EVIDENCE_SLOT_DATABASE must name a database of letters, digits and _ (with {slot})");   // it goes into a SQL literal
    }

    // The first KEY=<digits> line, as doctor.sh's sed read it; 0 and anything not a number are no slot.
    private static int? SlotNumber(string content, string key)
    {
        foreach (var line in content.Split('\n'))
        {
            var text = line.TrimEnd('\r');
            if (text.StartsWith(key + "=", StringComparison.Ordinal) && DigitsOnly.IsMatch(text[(key.Length + 1)..]))
            {
                return int.TryParse(text[(key.Length + 1)..], NumberStyles.None, CultureInfo.InvariantCulture, out var number) && number >= 1 ? number : null;
            }
        }

        return null;
    }

    [GeneratedRegex("^[A-Za-z0-9_]+\\z", RegexOptions.CultureInvariant, matchTimeoutMilliseconds: 100)]
    private static partial Regex DatabaseName { get; }

    [GeneratedRegex("^[0-9]+\\z", RegexOptions.CultureInvariant, matchTimeoutMilliseconds: 100)]
    private static partial Regex DigitsOnly { get; }
}
