namespace Aw.Context.Core.Concepts;

/// <summary>What a concept holds.</summary>
/// <param name="Features">Member features.</param>
/// <param name="Records">Member records.</param>
/// <param name="Decisions">Member decisions.</param>
/// <param name="Code">The distinct code nodes reached through the member features' 'implements' edges.</param>
public sealed record ConceptSize(int Features, int Records, int Decisions, int Code);
