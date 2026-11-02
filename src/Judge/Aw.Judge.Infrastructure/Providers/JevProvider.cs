using System.Net.Http.Headers;
using System.Text.Json.Nodes;
using Aw.Judge.Core.Config;
using Aw.Judge.Core.Evaluation;
using Aw.Judge.Core.Ports;
using Aw.Judge.Core.Providers;
using Aw.Judge.Core.Questions;
using Aw.Judge.Core.Secrets;
using Aw.Kernel.Json;

namespace Aw.Judge.Infrastructure.Providers;

/// <summary>
/// Jev, the TypeSafe API (POST https://api.typesafe.ai/v1/systemone), for the text classes: one 'choice' question per
/// call whose options are the question's outputs, each with its criterion (or its own name). The API key comes from
/// the secret source; without one Jev is unavailable ('no-api-key'), which is not a failure. Everything taken from the
/// input leaves the machine for a third party, so both the request's state and its instructions (the question's prompt,
/// which embeds input fields word for word) are redacted, then capped at the question's own cap (QuestionRef.InputCapChars), else at
/// config.json's providers.inputCapChars (the catalog sets it on the question), else at ProvidersConfig.DefaultInputCapChars; over the cap
/// only the longest strings are cut (InputCap). A non-success status is
/// errored ('http-NNN'); past the budget, the key read included, it is unavailable ('timeout'); unreachable is unavailable ('network-error').
/// </summary>
/// <param name="http">Sends the request (owned: disposed with the provider).</param>
/// <param name="secrets">Reads the API key.</param>
/// <param name="redactor">Strips secrets from the input before it leaves the machine.</param>
internal sealed partial class JevProvider(HttpClient http, ISecretSource secrets, IInputRedactor redactor) : IModelProvider, IDisposable
{
    /// <summary>The model.</summary>
    public const string Model = Core.Batching.JevModel.Id;

    /// <summary>The API's choice-option limit.</summary>
    public const int MaxOptions = 255;

    private const string QuestionId = "decision";

    // The least state a call is sent, and what the request's own JSON (model, keys, quotes) takes out of the cap.
    private const int MinStateChars = 1_000;

    private const int EnvelopeChars = 500;

    private static readonly Uri _endpoint = new("https://api.typesafe.ai/v1/systemone");

    /// <inheritdoc />
    public ProviderName Name => ProviderName.Jev;

    /// <inheritdoc />
    public IReadOnlySet<ContentClass> Classes { get; } = new HashSet<ContentClass>(ContentClasses.Text);

    /// <inheritdoc />
    public async ValueTask<ProviderResult> Decide(QuestionRef question, JsonNode? input, TimeSpan budget, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(question);
        cancellationToken.ThrowIfCancellationRequested();
        if (Refused(question) is { } refused)
        {
            return refused;
        }

        // The key read is inside the budget: with no key in the environment it asks the keychain, which can sit on its
        // unlock prompt, and that wait must end at the budget like the call itself.
        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        deadline.CancelAfter(budget);
        try
        {
            if (await secrets.Read(SecretName.TypeSafeApiKey, deadline.Token).ConfigureAwait(false) is not { } key)
            {
                return new ProviderUnavailable(ProviderReasons.NoApiKey);
            }

            var cap = question.InputCapChars ?? ProvidersConfig.DefaultInputCapChars;
            using var request = Request(question, input, cap, key);
            using var response = await http.SendAsync(request, deadline.Token).ConfigureAwait(false);
            if (!response.IsSuccessStatusCode)
            {
                return new ProviderErrored(ProviderReasons.Http((int)response.StatusCode));
            }

            var body = await response.Content.ReadAsByteArrayAsync(deadline.Token).ConfigureAwait(false);
            return Read(body, question);
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

    // What Jev can't take, decided before anything is read or sent: too many options, or a question that needs reasons (Jev only classifies).
    private static ProviderResult? Refused(QuestionRef question) =>
        question.Outputs.Count > MaxOptions ? new ProviderErrored(ProviderReasons.TooManyOptions)
        : question.ExtraProperties?.ContainsKey(ResolutionCheckQuestion.Reasons) == true ? new ProviderUnavailable(ProviderReasons.CannotExplain)
        : null;

    /// <inheritdoc />
    public void Dispose() => http.Dispose();

    // Jev is a third party: it gets what decision_details would store, the redacted input capped the same way, unless the
    // question sets a cap of its own (model-pick sends a whole pack).
    private JsonNode? CappedState(JsonNode? input, int cap) => JsonNode.Parse(InputCap.Write(redactor.Redact(input), cap, WireJson.Options));

    // The prompt carries the input's text too (ask-check's transcript tail, a rule-check hunk): redacted, then capped.
    private string CappedInstructions(string prompt, int cap) =>
        InputCap.Truncate(redactor.Redact(JsonValue.Create(prompt))?.GetValue<string>() ?? "", cap);

    // State and instructions together fit the cap: the instructions take at most half of it, the state the rest less the
    // criteria and the JSON around them. Choose's instructions leave its facts out (they are in the state once).
    private HttpRequestMessage Request(QuestionRef question, JsonNode? input, int cap, string key)
    {
        var instructions = CappedInstructions(question.PromptWithoutInput ?? question.Prompt, cap / 2);
        var criteriaChars = question.Outputs.Sum(option => option.Length + (question.Criteria?.GetValueOrDefault(option) ?? option).Length + 8);
        var stateCap = Math.Max(MinStateChars, cap - instructions.Length - criteriaChars - EnvelopeChars);
        return Request(question, CappedState(input, stateCap), instructions, key);
    }

    private static HttpRequestMessage Request(QuestionRef question, JsonNode? input, string instructions, string key)
    {
        var criteria = question.Outputs.ToDictionary(
            option => option,
            option => question.Criteria?.GetValueOrDefault(option) ?? option,
            StringComparer.Ordinal);
        var body = new JevRequest(
            input,
            Model,
            new Dictionary<string, JevChoiceQuestion>(StringComparer.Ordinal) { [QuestionId] = new("choice", instructions, criteria) });
        var content = new ByteArrayContent(AwJson.Serialize(body, WireJson.Options));
        content.Headers.ContentType = new MediaTypeHeaderValue("application/json");
        var request = new HttpRequestMessage(HttpMethod.Post, _endpoint) { Content = content };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", key);
        return request;
    }

    private static ProviderResult Read(byte[] body, QuestionRef question)
    {
        var response = AwJson.Deserialize<JevResponse>(body, WireJson.Options);
        if (response.IsError || response.Value.Answers?.GetValueOrDefault(QuestionId) is not { Type: "choice", Choice: { } choice } answer)
        {
            return new ProviderErrored(ProviderReasons.UnparseableAnswer);
        }

        if (!question.Outputs.Contains(choice, StringComparer.Ordinal))
        {
            return new ProviderErrored(ProviderReasons.OutOfEnum);
        }

        // A question that asks for a confidence (choose) never takes a missing or out-of-range one as certain.
        double confidence;
        if (answer.Confidence is >= 0 and <= 1)
        {
            confidence = answer.Confidence.Value;
        }
        else if (question.AskConfidence)
        {
            return new ProviderErrored(ProviderReasons.UnparseableAnswer);
        }
        else
        {
            confidence = AgentAnswer.DefaultConfidence;
        }

        return new ProviderDecided(choice, confidence, ProviderReasons.Model) { Probabilities = answer.Probabilities };
    }
}
