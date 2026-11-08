namespace Aw.JevWire;

/// <summary>A 'noul' question: a yes/no answered as a number.</summary>
/// <param name="Instructions">The prompt.</param>
public sealed record JevBatchNoul(string Instructions) : JevBatchQuestion(Instructions);
