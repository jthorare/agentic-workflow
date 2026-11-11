using System.Globalization;

namespace Aw.Sindri.Infrastructure.Locks;

/// <summary>Who holds the heavy-job lock's slots, in the words the waiter and the refusal use.</summary>
internal static class HeavySlotHolders
{
    /// <summary>'slot 1/2 held by 4242 sindri context-map-build, slot 2/2 held by 77 heavy …': what the waiter and the refusal say.</summary>
    internal static string Describe(string[] slots) => string.Join(", ", slots.Select((slot, i) =>
    {
        var owner = Path.Combine(slot, DirectoryHeavySlots.OwnerFile);
        var by = File.Exists(owner) ? SafeRead(owner) : "an unknown holder";
        return string.Create(CultureInfo.InvariantCulture, $"slot {i + 1}/{slots.Length} held by {by}");
    }));

    private static string SafeRead(string owner)
    {
        try
        {
            var parts = File.ReadAllText(owner).Trim().Split(' ', 3);
            return parts.Length == 3 ? $"{parts[0]} ({parts[2]}, since {parts[1]})" : string.Join(' ', parts);
        }
        catch (IOException)
        {
            return "an unknown holder";
        }
    }
}
