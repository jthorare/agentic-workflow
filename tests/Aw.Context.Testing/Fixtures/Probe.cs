namespace Aw.Context.Testing.Fixtures;

/// <summary>One probe question over the fixture (spec §9.2 item 3) and its expected anchors.</summary>
/// <param name="Id">A stable slug.</param>
/// <param name="Question">The question as a person would ask it.</param>
/// <param name="Expect">What a correct answer contains.</param>
public sealed record Probe(string Id, string Question, ProbeExpectation Expect);
