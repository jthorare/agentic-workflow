namespace Aw.Judge.Infrastructure.Providers;

/// <summary>A 'noul' question: a yes/no answered as a number.</summary>
/// <param name="Instructions">The prompt.</param>
internal sealed record JevBatchNoul(string Instructions) : JevBatchQuestion(Instructions);
