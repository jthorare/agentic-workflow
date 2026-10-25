using Aw.Context.Core.Graph;

namespace Aw.Context.Core.Concepts;

/// <summary>The reserved concept for every feature, record and decision no other concept takes: nothing is dropped.</summary>
/// <param name="Id">'concept:other'.</param>
/// <param name="Name">'Other'.</param>
/// <param name="Size">What it holds.</param>
public sealed record ConceptOther(NodeKey Id, string Name, ConceptSize Size);
