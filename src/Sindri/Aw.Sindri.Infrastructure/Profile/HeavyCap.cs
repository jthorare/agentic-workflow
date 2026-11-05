using System.Globalization;
using Aw.Sindri.Core.Profile;

namespace Aw.Sindri.Infrastructure.Profile;

/// <summary>The box's heavy-slot cap, as the heavy wrapper reads it (blocks-over-MCP spec BM-1): the cap file $AW_STATE_DIR/heavy-slots-cap wins over AW_HEAVY_SLOTS.</summary>
internal static class HeavyCap
{
    /// <summary>The slots a box with no cap file and no AW_HEAVY_SLOTS has (the heavy wrapper's default).</summary>
    private const int DefaultSlots = 1;

    /// <summary>The cap now: <see cref="Slots"/> as a box limit.</summary>
    /// <param name="environment">Reads an environment variable.</param>
    /// <param name="stateRoot">The state root (AW_STATE_DIR or ~/.agentic-workflow).</param>
    internal static BoxLimits Read(Func<string, string?> environment, string stateRoot) => new(Slots(environment, stateRoot));

    /// <summary>The one slot-count rule: the cap file when it holds a whole number of 1 or more, else AW_HEAVY_SLOTS read as the lock reads it (unset or empty is one slot, anything but a whole number of 1 or more is one slot). The heavy lock and the profile's box limit both read it here.</summary>
    /// <param name="environment">Reads an environment variable.</param>
    /// <param name="stateRoot">The state root (AW_STATE_DIR or ~/.agentic-workflow).</param>
    /// <returns>The slot count, at least 1.</returns>
    internal static int Slots(Func<string, string?> environment, string stateRoot)
    {
        ArgumentNullException.ThrowIfNull(environment);
        if (FromFile(Path.Combine(stateRoot, "heavy-slots-cap")) is { } cap)
        {
            return cap;
        }

        var text = environment("AW_HEAVY_SLOTS");
        return string.IsNullOrEmpty(text) ? DefaultSlots : Count(text) ?? 1;
    }

    /// <summary>The meta reserve now: the whole number in $AW_STATE_DIR/heavy-meta-reserve (0 when the file is missing or holds anything else), at most <paramref name="slots"/>, as scripts/heavy.sh and config/lib/heavy-config.sh read it. Slots 1 to the reserve are for meta jobs only.</summary>
    /// <param name="stateRoot">The state root (AW_STATE_DIR or ~/.agentic-workflow).</param>
    /// <param name="slots">The slot count the reserve is clamped to.</param>
    /// <returns>The reserve, from 0 to <paramref name="slots"/>.</returns>
    internal static int Reserve(string stateRoot, int slots)
    {
        var text = ReadText(Path.Combine(stateRoot, "heavy-meta-reserve"));
        // All digits but too big for an int is still a number: clamp it to the slot count, as bash does; anything else is 0.
        return text is { Length: > 0 } && text.All(char.IsAsciiDigit) ? (int.TryParse(text, NumberStyles.None, CultureInfo.InvariantCulture, out var n) ? Math.Min(n, slots) : slots) : 0;
    }

    private static int? FromFile(string path) => ReadText(path) is { } text ? Count(text) : null;

    private static string? ReadText(string path)
    {
        try
        {
            return File.Exists(path) ? File.ReadAllText(path).Trim() : null;
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
