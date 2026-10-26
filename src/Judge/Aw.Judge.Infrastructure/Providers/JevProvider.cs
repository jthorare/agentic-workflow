using System.Net.Http.Headers;
using System.Text.Json.Nodes;
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
/// which embeds input fields word for word) are redacted, then capped like the stored input (InputCap). A non-success status is
/// errored ('http-NNN'); past the budget, the key read included, it is unavailable ('timeout'); unreachable is unavailable ('network-error').
/// </summary>
/// <param name="http">Sends the request (owned: disposed with the provider).</param>
/// <param name="secrets">Reads the API key.</param>
/// <param name="redactor">Strips secrets from the input before it leaves the machine.</param>
internal sealed partial class JevProvider(HttpClient http, ISecretSource secrets, IInputRedactor redactor) : IModelProvider, IDisposable
{
    /// <summary>The model.</summary>
    public const string Model = "jev-1.13.0";

    /// <summary>The API's choice-option limit.</summary>
    public const int MaxOptions = 255;

    private const string QuestionId = "decision";

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

            using var request = Request(question, State(input), Instructions(question.Prompt), key);
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

    // Jev is a third party: it gets what decision_details would store, the redacted input capped the same way.
    private JsonNode? State(JsonNode? input) => JsonNode.Parse(InputCap.Write(redactor.Redact(input), InputCap.MaxChars, WireJson.Options));

    // The prompt carries the input's text too (ask-check's transcript tail, a rule-check hunk): redacted, then capped.
    private string Instructions(string prompt) =>
        InputCap.Truncate(redactor.Redact(JsonValue.Create(prompt))?.GetValue<string>() ?? "", InputCap.MaxChars);

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
