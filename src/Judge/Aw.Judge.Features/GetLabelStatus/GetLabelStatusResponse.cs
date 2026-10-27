namespace Aw.Judge.Features.GetLabelStatus;

/// <summary>The GetLabelStatus response.</summary>
/// <param name="Questions">One entry per question that has eval items, by question name.</param>
public sealed record GetLabelStatusResponse(IReadOnlyList<QuestionLabelStatus> Questions);
