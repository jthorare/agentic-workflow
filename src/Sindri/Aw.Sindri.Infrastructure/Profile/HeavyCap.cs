using System.Globalization;
using Aw.Sindri.Core.Profile;

namespace Aw.Sindri.Infrastructure.Profile;

/// <summary>The box's heavy-slot cap, as the heavy wrapper reads it (blocks-over-MCP spec BM-1): the cap file $AW_STATE_DIR/heavy-slots-cap wins over AW_HEAVY_SLOTS.</summary>
internal static class HeavyCap
{
    /// <summary>The slots a box with no cap file and no AW_HEAVY_SLOTS has (the heavy wrapper's default).</summary>
    private const int DefaultSlots = 1;

    /// <summary>The cap now: the cap file when it holds a whole number of 1 or more, else AW_HEAVY_SLOTS read as the lock reads it (unset or empty is one slot, anything but a whole number of 1 or more is one slot).</summary>
    /// <param name="environment">Reads an environment variable.</param>
    /// <param name="stateRoot">The state root (AW_STATE_DIR or ~/.agentic-workflow).</param>
    internal static BoxLimits Read(Func<string, string?> environment, string stateRoot)
    {
        ArgumentNullException.ThrowIfNull(environment);
        if (FromFile(Path.Combine(stateRoot, "heavy-slots-cap")) is { } cap)
        {
            return new BoxLimits(cap);
        }

        var text = environment("AW_HEAVY_SLOTS");
        return new BoxLimits(string.IsNullOrEmpty(text) ? DefaultSlots : Count(text) ?? 1);
    }

    private static int? FromFile(string path)
    {
        try
        {
            return File.Exists(path) ? Count(File.ReadAllText(path).Trim()) : null;
        }
        catch (IOException)
        {
            return null;   // a cap file that cannot be read is no cap file
        }
        catch (UnauthorizedAccessException)
        {
            return null;
        }
    }

    private static int? Count(string text) => int.TryParse(text, NumberStyles.None, CultureInfo.InvariantCulture, out var n) && n >= 1 ? n : null;
}
