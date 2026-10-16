namespace Aw.Sindri.Core.Requests;

/// <summary>A request kind in words, for labels and timeline lines (UI spec E-7).</summary>
public static class RequestKindWords
{
    /// <summary>The words for <paramref name="kind"/>: 're-scope', 'add task', 'note' or 'hand back'.</summary>
    /// <param name="kind">The kind.</param>
    /// <returns>The words.</returns>
    public static string Of(RequestKind kind) => kind switch
    {
        RequestKind.Rescope => "re-scope",
        RequestKind.AddTask => "add task",
        RequestKind.Note => "note",
        RequestKind.Handback => "hand back",
        _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, "not a request kind"),
    };
}
