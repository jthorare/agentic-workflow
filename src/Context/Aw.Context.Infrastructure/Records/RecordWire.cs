using Aw.Context.Core.Records;
using Aw.Context.Core.Sources;

namespace Aw.Context.Infrastructure.Records;

/// <summary>The words the store keeps for its enums: the same ones the JSON wire uses, listed here so a read never goes through reflection.</summary>
internal static class RecordWire
{
    private static readonly string[] _kinds = ["doc", "issue", "comment", "project", "message", "code", "pr"];
    private static readonly string[] _states = ["open", "closed", "merged", "draft", "canceled", "done"];
    private static readonly string[] _links = ["parent", "blocks", "duplicates", "relates", "closes", "attachment", "review-of", "replies-to", "in-project", "touches", "led-by"];
    private static readonly string[] _phases = ["recent", "backfill", "incremental", "reconcile"];
    private static readonly string[] _spaces = ["chunk", "node"];

    internal static string Of(RecordKind kind) => _kinds[(int)kind];

    internal static string Of(RecordState state) => _states[(int)state];

    internal static string Of(RecordLinkKind kind) => _links[(int)kind];

    internal static string Of(SyncPhase phase) => _phases[(int)phase];

    internal static string Of(VectorSpace space) => _spaces[(int)space];

    internal static RecordKind Kind(string text) => (RecordKind)Index(_kinds, text);

    internal static RecordState State(string text) => (RecordState)Index(_states, text);

    internal static RecordLinkKind Link(string text) => (RecordLinkKind)Index(_links, text);

    internal static SyncPhase Phase(string text) => (SyncPhase)Index(_phases, text);

    internal static VectorSpace Space(string text) => (VectorSpace)Index(_spaces, text);

    private static int Index(string[] words, string text) =>
        Array.IndexOf(words, text) is var i and >= 0 ? i : throw new InvalidDataException($"'{text}' isn't a stored record word.");
}
