using System.Text.Json.Serialization;

namespace Aw.Judge.Core.Questions;

/// <summary>
/// model-pick's input (docs/csharp/judge.md): a step, its brief, the pack's files and facts, and the models to choose
/// from, cheapest first. The pack filters run on it before any provider sees it; the last four members are set by them
/// and ignored when a caller sends them.
/// </summary>
/// <param name="Step">The step's block id and state id (step.interview.questions, questions).</param>
/// <param name="Kind">draft, challenge, questions, apply, other or concept-scan.</param>
/// <param name="Brief">The rendered step brief (may be empty).</param>
/// <param name="Files">The pack bundle's files and the written facts, each as path and text.</param>
/// <param name="Candidates">One to 32 models after the constraints, cheapest first; their keys are the decision's allowed values.</param>
/// <param name="DenyPaths">The profile's index.denyPaths globs; files and facts that match are dropped.</param>
/// <param name="History">Per candidate, this recipe step's record.</param>
/// <param name="Previous">For an escalation, what just failed.</param>
/// <param name="Omitted">Set by the filters: the files left out by the size cap, named with their sizes.</param>
/// <param name="Cut">Set by the filters: the file cut at a line to fit.</param>
/// <param name="Withheld">Set by the filters: how many files were dropped as denied or secret.</param>
public sealed record ModelPickInput(
    string Step,
    string Kind,
    string Brief,
    IReadOnlyList<ModelPickFile> Files,
    IReadOnlyList<ModelPickCandidate> Candidates,
    IReadOnlyList<string>? DenyPaths = null,
    IReadOnlyList<ModelPickHistory>? History = null,
    ModelPickPrevious? Previous = null,
    IReadOnlyList<ModelPickOmitted>? Omitted = null,
    IReadOnlyList<ModelPickCut>? Cut = null,
    ModelPickWithheld? Withheld = null)
{
    /// <summary>Set by the filters: the files dropped as denied or secret, named. Never serialized, so never sent; only the stored form keeps it.</summary>
    [JsonIgnore]
    public IReadOnlyList<ModelPickOmitted>? Dropped { get; init; }
}
