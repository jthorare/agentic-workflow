namespace Aw.Sindri.Core.Judge;

/// <summary>How a judge choose call ended.</summary>
public enum ChooseStatus
{
    /// <summary>A provider chose one of the options with enough confidence.</summary>
    Decided = 1,

    /// <summary>The judge escalated: below the threshold, no provider decided, or a rule timed out. Not an error: the route goes to its 'uncertain' outcome.</summary>
    Escalated = 2,

    /// <summary>The judge could not answer: invalid input, a disabled question, an error, a timeout or a missing program.</summary>
    Failed = 3,
}
