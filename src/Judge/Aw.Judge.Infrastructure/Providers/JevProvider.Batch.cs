using System.Net.Http.Headers;
using Aw.Judge.Core.Batching;
using Aw.Judge.Core.Providers;
using Aw.Judge.Core.Secrets;
using Aw.Kernel.Json;

namespace Aw.Judge.Infrastructure.Providers;

// The batched call: one POST /v1/systemone carries any number of typed questions (the API takes a map; choice at most
// 255 options, score at most 10 levels). Same rules as Decide: the key read is inside the budget, everything taken from
// the state and the question texts is redacted and capped, a non-success status is errored, a timeout or an unreachable
// API is unavailable.
internal sealed partial class JevProvider
{
    /// <summary>The API's score-level limit.</summary>
    public const int MaxLevels = 10;

    /// <summary>Asks every question of the batch in one call.</summary>
    /// <param name="batch">The state and the questions.</param>
    /// <param name="cancellationToken">Cancels the call.</param>
    /// <returns>
    /// <see cref="BatchDecided"/> with the usable answers (a question the API answered in an unusable shape is left out);
    /// <see cref="ProviderUnavailable"/> without a key, past the budget or when the API can't be reached;
    /// <see cref="ProviderErrored"/> for an error status, a body that isn't an answer, or a question the API can't take.
    /// </returns>
    public async ValueTask<ProviderResult> Batch(BatchRequest batch, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(batch);
        cancellationToken.ThrowIfCancellationRequested();
        if (Invalid(batch) is { } invalid)
        {
            return new ProviderErrored(invalid);
        }

        if (batch.Questions.Count == 0)
        {
            return new BatchDecided(new Dictionary<string, BatchAnswer>(StringComparer.Ordinal));
        }

        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        deadline.CancelAfter(batch.Budget);
        try
        {
            if (await secrets.Read(SecretName.TypeSafeApiKey, deadline.Token).ConfigureAwait(false) is not { } key)
            {
                return new ProviderUnavailable(ProviderReasons.NoApiKey);
            }

            using var request = BatchMessage(batch, key);
            using var response = await http.SendAsync(request, deadline.Token).ConfigureAwait(false);
            if (!response.IsSuccessStatusCode)
            {
                return new ProviderErrored(ProviderReasons.Http((int)response.StatusCode));
            }

            var body = await response.Content.ReadAsByteArrayAsync(deadline.Token).ConfigureAwait(false);
            return ReadBatch(body, batch);
        }
        catch (OperationCanceledException) when (deadline.IsCancellationRequested && !cancellationToken.IsCancellationRequested)
        {
            return new ProviderUnavailable(ProviderUnavailable.Timeout);
        }
        catch (HttpRequestException)
        {
            return new ProviderUnavailable(ProviderReasons.NetworkError);
        }
    }

    private static string? Invalid(BatchRequest batch)
    {
        if (new HashSet<string>(batch.Questions.Select(q => q.Key), StringComparer.Ordinal).Count != batch.Questions.Count)
        {
            return ProviderReasons.DuplicateQuestion;
        }

        return batch.Questions.Any(q => q is ChoiceBatchQuestion { Criteria.Count: > MaxOptions })
            ? ProviderReasons.TooManyOptions
            : batch.Questions.Any(q => q is ScoreBatchQuestion { Levels.Count: > MaxLevels }) ? ProviderReasons.TooManyLevels : null;
    }

    private HttpRequestMessage BatchMessage(BatchRequest batch, string key)
    {
        var questions = batch.Questions.ToDictionary(q => q.Key, WireQuestion, StringComparer.Ordinal);
        var body = new JevBatchRequest(State(batch.State), Model, questions);
        var content = new ByteArrayContent(AwJson.Serialize(body, WireJson.Options));
        content.Headers.ContentType = new MediaTypeHeaderValue("application/json");
        var request = new HttpRequestMessage(HttpMethod.Post, _endpoint) { Content = content };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", key);
        return request;
    }

    // The question texts leave the machine too: redacted and capped like the state.
    private JevBatchQuestion WireQuestion(BatchQuestion question) => question switch
    {
        ChoiceBatchQuestion choice => new JevBatchChoice(
            Instructions(choice.Prompt),
            choice.Criteria.ToDictionary(pair => pair.Key, pair => Instructions(pair.Value), StringComparer.Ordinal)),
        ScoreBatchQuestion score => new JevBatchScore(Instructions(score.Prompt), [.. score.Levels.Select(Instructions)]),
        _ => new JevBatchNoul(Instructions(question.Prompt)),
    };

    private static ProviderResult ReadBatch(byte[] body, BatchRequest batch)
    {
        var response = AwJson.Deserialize<JevBatchResponse>(body, WireJson.Options);
        if (response.IsError)
        {
            return new ProviderErrored(ProviderReasons.UnparseableAnswer);
        }

        var answers = new Dictionary<string, BatchAnswer>(StringComparer.Ordinal);
        foreach (var question in batch.Questions)
        {
            if (response.Value.Answers?.GetValueOrDefault(question.Key) is { } raw && Answer(question, raw) is { } answer)
            {
                answers[question.Key] = answer;
            }
        }

        return new BatchDecided(
            answers,
            response.Value.Usage is { } usage ? new BatchUsage(Tokens(usage.InputTokens), Tokens(usage.OutputTokens)) : null);
    }

    // Null when the answer is not of the question's kind or not in its range: the question is then undecided.
    private static BatchAnswer? Answer(BatchQuestion question, JevBatchAnswer raw)
    {
        var confidence = raw.Confidence is >= 0 and <= 1 ? raw.Confidence.Value : AgentAnswer.DefaultConfidence;
        var probabilities = raw.Probabilities ?? new Dictionary<string, double>(StringComparer.Ordinal);
        return (question, raw) switch
        {
            (ChoiceBatchQuestion choice, { Type: "choice", Choice: { } picked }) when choice.Criteria.ContainsKey(picked)
                => new ChoiceBatchAnswer(picked, confidence, probabilities),
            (NoulBatchQuestion, { Type: "noul", Noul: { } noul }) when double.IsFinite(noul) => new NoulBatchAnswer(noul),
            (ScoreBatchQuestion, { Type: "score", Score: { } score }) when double.IsFinite(score) => new ScoreBatchAnswer(score, confidence, probabilities),
            _ => null,
        };
    }

    private static long Tokens(double? count) => count is { } n && double.IsFinite(n) && n >= 0 ? (long)n : 0;
}
