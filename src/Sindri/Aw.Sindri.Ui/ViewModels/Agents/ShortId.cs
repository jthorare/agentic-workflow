namespace Aw.Sindri.Ui.ViewModels.Agents;

/// <summary>A session id as a row or sheet title shows it: its first 8 characters, without a separator the cut left at the end ('pid-411-' reads 'pid-411').</summary>
internal static class ShortId
{
    private const int Length = 8;

    /// <summary>The id cut to 8 characters with no trailing hyphen.</summary>
    /// <param name="id">The session id as served.</param>
    internal static string Of(string id) => id.Length > Length ? id[..Length].TrimEnd('-') : id;
}
