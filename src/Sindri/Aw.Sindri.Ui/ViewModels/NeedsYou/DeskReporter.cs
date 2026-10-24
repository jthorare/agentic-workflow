using System.Text;
using Aw.Kernel;
using Aw.Sindri.Ui.Api;
using Aw.Sindri.Ui.Api.Workflow;
using Aw.Sindri.Ui.Platform;

namespace Aw.Sindri.Ui.ViewModels.NeedsYou;

/// <summary>
/// The one way the app reports an open desk run (the action bar's Approve, Send back and Drop): the run is read from the instance, and a
/// report is sent once and never retried by the app. A press while a report for the same instance is in flight does nothing; a refusal
/// leaves it pressable (the user may press again).
/// </summary>
internal sealed class DeskReporter(ISindriApi api, Func<Uri?> apiUrl, ILauncher? launcher = null)
{
    /// <summary>Opens a link or a file for a review button.</summary>
    internal ILauncher Launcher { get; } = launcher ?? NullLauncher.Instance;

    private const string AnswersKey = "fact:answers";
    private readonly HashSet<string> _inFlight = new(StringComparer.Ordinal);

    /// <summary>Reads the instance's open desk run.</summary>
    /// <param name="instanceId">The instance.</param>
    /// <returns>The run, or null when the instance can't be read or has no open desk run.</returns>
    internal async Task<DeskTarget?> Load(string instanceId)
    {
        if (apiUrl() is not { } url)
        {
            return null;
        }

        var read = await api.Instance(url, instanceId, CancellationToken.None).ConfigureAwait(true);
        var verbs = VerbsOf(read.IsSuccess ? read.Value.AllowedCommands : null);
        if (read.IsError || (read.Value.Current.Desk is null && verbs.Count == 0))
        {
            return null;   // not an open desk run, and no recipe verb to press either
        }

        var desk = read.Value.Current.Desk;

        var complete = read.Value.Current.Status.Commands.FirstOrDefault(c => c.Verb == StatusVerb.Complete);
        var state = await StateOf(url, read.Value).ConfigureAwait(true);
        var card = await MergeCardOf(url, read.Value).ConfigureAwait(true);
        var questions = await QuestionsOf(url, read.Value).ConfigureAwait(true);
        return Target(new DeskTarget(instanceId, desk?.Run ?? string.Empty, complete, state?.Why, OutcomeText(state, "approve"), OutcomeText(state, "revise"), desk?.Recommended, card, questions) { Verbs = verbs }, read.Value);
    }

    // The ask step's questions; null for a desk that does not ask.
    private async Task<IReadOnlyList<DeskQuestion>?> QuestionsOf(Uri url, InstanceDetail detail) =>
        detail.Current.Desk is { } desk && AsksQuestions(desk, detail.Current.Status.Inputs) ? await Questions(url, detail.Id).ConfigureAwait(true) : null;

    // The ask step: the person's answers are the desk's required output and the questions are the escalation fact it was given.
    private static bool AsksQuestions(DeskForm desk, IReadOnlyList<StatusInput> inputs) =>
        desk.Outputs.Any(o => string.Equals(o.Key, AnswersKey, StringComparison.Ordinal)) && inputs.Any(i => string.Equals(i.Key, DeskQuestionReader.Key, StringComparison.Ordinal));

    // The whole list comes from the fact read: the instance's fact row only carries a one-line summary.
    private async Task<IReadOnlyList<DeskQuestion>> Questions(Uri url, string instanceId)
    {
        var fact = await api.InstanceFact(url, instanceId, DeskQuestionReader.Key, CancellationToken.None).ConfigureAwait(true);
        return fact.IsError ? [] : DeskQuestionReader.Of(fact.Value);
    }

    /// <summary>The fact "answers" for a list of answered questions: [{id, question, answer}] in the order asked.</summary>
    /// <param name="answered">The questions with their answers.</param>
    /// <returns>The JSON text.</returns>
    internal static string AnswersJson(IEnumerable<(string Id, string Question, string Answer)> answered)
    {
        ArgumentNullException.ThrowIfNull(answered);
        using var stream = new MemoryStream();
        using (var writer = new System.Text.Json.Utf8JsonWriter(stream))
        {
            writer.WriteStartArray();
            foreach (var (id, question, answer) in answered)
            {
                writer.WriteStartObject();
                writer.WriteString("id", id);
                writer.WriteString("question", question);
                writer.WriteString("answer", answer);
                writer.WriteEndObject();
            }

            writer.WriteEndArray();
        }

        return Encoding.UTF8.GetString(stream.ToArray());
    }

    // What the step asks the person to read, the session that runs it and the code behind its state.
    private static DeskTarget Target(DeskTarget target, InstanceDetail detail) => target with
    {
        Address = detail.Address,
        Reviews = detail.Current.Desk?.Reviews ?? [],
        SessionRef = detail.Current.Status.Run?.Session?.Ref(),
        ReasonCode = detail.Current.Status.ReasonCode,
    };

    // The recipe state the instance is in, for its why and its outcomes; null when the recipe cannot be read.
    private async Task<RecipeStateView?> StateOf(Uri url, InstanceDetail detail)
    {
        var recipe = await api.Recipe(url, detail.Recipe.Id, hash: null, CancellationToken.None).ConfigureAwait(true);
        return recipe.IsSuccess ? recipe.Value.States.FirstOrDefault(s => string.Equals(s.State, detail.Current.State, StringComparison.Ordinal)) : null;
    }

    // The pull request card, when the instance lists the fact.
    private async Task<MergeCardViewModel?> MergeCardOf(Uri url, InstanceDetail detail) =>
        detail.Facts.Any(f => string.Equals(f.Key, PrStatusReader.Key, StringComparison.Ordinal)) ? await MergeCard(url, detail.Id).ConfigureAwait(true) : null;

    // The recipe verbs of the instance: one per distinct verb the engine serves, in its order.
    private static List<DeskVerb> VerbsOf(IReadOnlyList<AllowedCommandView>? commands) =>
        [.. (commands ?? [])
            .Where(c => string.Equals(c.ActionId, "ApplyCommand:command", StringComparison.Ordinal) && !string.IsNullOrWhiteSpace(c.Verb))
            .DistinctBy(c => c.Verb, StringComparer.Ordinal)
            .Select(c => new DeskVerb(c.Verb!, c.Cli, c.Destructive == true))];   // why: filtered to a verb above

    /// <summary>Sends a recipe verb, once: a press while another report for the instance is in flight does nothing.</summary>
    /// <param name="target">The shown request.</param>
    /// <param name="verb">The verb.</param>
    /// <returns>The answer, or null when a report for the instance was in flight or no endpoint is known.</returns>
    internal async Task<Result<ApplyCommandAccepted>?> ApplyVerb(DeskTarget target, string verb)
    {
        ArgumentNullException.ThrowIfNull(target);
        if (apiUrl() is not { } url || !_inFlight.Add(target.InstanceId))
        {
            return null;
        }

        try
        {
            return await api.ApplyCommand(url, target.InstanceId, ApplyCommandBody.ForVerb(target.InstanceId, verb), CancellationToken.None).ConfigureAwait(true);
        }
        finally
        {
            _ = _inFlight.Remove(target.InstanceId);
        }
    }

    // The merge stop's pull request, when the instance lists it and it reads; a fact that cannot be read leaves the card out, never the request.
    private async Task<MergeCardViewModel?> MergeCard(Uri url, string instanceId)
    {
        var fact = await api.InstanceFact(url, instanceId, PrStatusReader.Key, CancellationToken.None).ConfigureAwait(true);
        if (fact.IsError)
        {
            return null;
        }

        var status = PrStatusReader.Of(fact.Value);
        return status.IsSuccess ? new MergeCardViewModel(status.Value, launcher) : null;
    }

    // What the state says an outcome does: the outcome named exactly, or (for send back) the first one that starts with the word, as the recipe words it.
    private static string? OutcomeText(RecipeStateView? state, string outcome) =>
        state?.OnOutcome?.FirstOrDefault(o => o.Outcome.StartsWith(outcome, StringComparison.OrdinalIgnoreCase) && o.Text.Length > 0)?.Text;

    /// <summary>True when a report for the instance is in flight.</summary>
    /// <param name="instanceId">The instance.</param>
    /// <returns>True while it is.</returns>
    internal bool IsInFlight(string instanceId) => _inFlight.Contains(instanceId);

    /// <summary>Sends the report; the guard is read and set before the first await.</summary>
    /// <param name="target">The open run.</param>
    /// <param name="outcome">The outcome (approve, revise, drop).</param>
    /// <param name="answers">The text sent as the fact "answers" (a send-back's reason); null for none.</param>
    /// <returns>The answer, or null when a report for the instance was in flight or no endpoint is known.</returns>
    internal async Task<Result<CompleteStepAccepted>?> Complete(DeskTarget target, string outcome, string? answers)
    {
        ArgumentNullException.ThrowIfNull(target);
        if (apiUrl() is not { } url || !_inFlight.Add(target.InstanceId))
        {
            return null;
        }

        try
        {
            var outputs = new Dictionary<string, byte[]>(StringComparer.Ordinal);
            if (answers is { Length: > 0 })
            {
                outputs[AnswersKey] = Encoding.UTF8.GetBytes(answers);
            }

            return await api.CompleteStep(url, target.InstanceId, new CompleteStepBody(target.InstanceId, target.Run, outcome, outputs), CancellationToken.None).ConfigureAwait(true);
        }
        finally
        {
            _ = _inFlight.Remove(target.InstanceId);
        }
    }
}
