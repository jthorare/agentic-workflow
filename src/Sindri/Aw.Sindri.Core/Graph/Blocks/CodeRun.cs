namespace Aw.Sindri.Core.Graph.Blocks;

/// <summary>One run of a 'code' node: the C# body, its input and the limits it runs under.</summary>
/// <param name="Code">The method body; it gets 'JsonNode? input' and returns any value.</param>
/// <param name="InputJson">The input as JSON ('null' when the node has none).</param>
/// <param name="Repo">The profile repo whose files the code may read, or null for none.</param>
/// <param name="Branch">The branch of that repo, or null for the checkout as it is.</param>
/// <param name="Timeout">How long it may run before it is killed.</param>
/// <param name="MemoryMb">How much managed heap it may use, in MiB.</param>
public sealed record CodeRun(string Code, string InputJson, string? Repo, string? Branch, TimeSpan Timeout, int MemoryMb);
