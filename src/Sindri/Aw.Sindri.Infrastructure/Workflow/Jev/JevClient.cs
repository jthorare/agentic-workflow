#pragma warning disable MA0182 // why: the graph block registry is not registered in DI yet (EmptyGraphBlockCatalog); remove this when it is
using System.Globalization;
using System.Net.Http.Headers;
using System.Text.Json;
using System.Text.Json.Nodes;
using Aw.JevWire;
using Aw.Kernel;
using Aw.Kernel.Json;
using Aw.Redaction;
using Aw.Sindri.Core;
using Aw.Sindri.Core.Graph.Blocks;

namespace Aw.Sindri.Infrastructure.Workflow.Jev;

/// <summary>
/// Jev over HTTP (POST /v1/systemone). Jev is a third party, so the text, the instructions and the criteria are redacted
/// first and the whole request is cut to 160,000 characters (<see cref="JevEndpoints.InputCapChars"/>). A failure carries
/// the judge's reason code in its message: 'no-api-key', 'network-error', 'http-NNN', 'timeout' or 'unparseable-answer'.
/// </summary>
/// <param name="http">Sends the request (built with redirects off, with the call's timeout).</param>
/// <param name="readKey">Reads the TypeSafe API key (the environment, else the judge's keychain entry); null when there is none.</param>
/// <param name="redactor">Strips secrets before the text leaves the machine.</param>
internal sealed class JevClient(HttpClient http, Func<CancellationToken, ValueTask<string?>> readKey, IRedactor redactor) : IJevPort
{
    private const int MinStateChars = 1_000;
    private const int EnvelopeChars = 500;

    private static readonly JsonSerializerOptions _options = AwJson.CreateOptions(JevClientJsonContext.Default);

    private static readonly Uri _endpoint = new(JevEndpoints.SystemOne);

    /// <inheritdoc />
    public async ValueTask<Result<JevOutcome>> Ask(JevCall call, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(call);
        var key = await readKey(cancellationToken).ConfigureAwait(false);
        if (string.IsNullOrEmpty(key))
        {
            return Fail("no-api-key");
        }

        var built = Body(call);
        if (built.IsError)
        {
            return built.Error;
        }

        var model = call.Model ?? JevEndpoints.Model;
        using var request = new HttpRequestMessage(HttpMethod.Post, _endpoint)
        {
            Content = new ByteArrayContent(AwJson.Serialize(built.Value, _options)),
        };
        request.Content.Headers.ContentType = new MediaTypeHeaderValue("application/json");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", key);
        try
        {
            using var response = await http.SendAsync(request, cancellationToken).ConfigureAwait(false);
            if (!response.IsSuccessStatusCode)
            {
                return Fail("http-" + ((int)response.StatusCode).ToString(CultureInfo.InvariantCulture));
            }

            var parsed = AwJson.Deserialize<JevBatchResponse>(await response.Content.ReadAsByteArrayAsync(cancellationToken).ConfigureAwait(false), _options);
            return parsed.IsError ? Fail("unparseable-answer") : Outcome(parsed.Value, model, call);
        }
        catch (HttpRequestException)
        {
            return Fail("network-error");
        }
        catch (TaskCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return Fail("timeout");
        }
    }

    private static Result<JevOutcome> Outcome(JevBatchResponse response, string model, JevCall call)
    {
        var answers = new Dictionary<string, JevAnswerValue>(StringComparer.Ordinal);
        foreach (var question in call.Questions)
        {
            if (response.Answers?.GetValueOrDefault(question.Name) is not { } a || !string.Equals(a.Type, question.Type, StringComparison.Ordinal))
            {
                return Fail("unparseable-answer");
            }

            answers[question.Name] = new JevAnswerValue(question.Type, a.Choice, a.Score, a.Noul, a.Confidence, a.Probabilities);
        }

        return new JevOutcome(model, answers, response.Usage?.InputTokens, response.Usage?.OutputTokens);
    }

    // The request, redacted and cut: instructions take at most half the cap, the state the rest less the criteria and the JSON around them.
    private Result<JevBatchRequest> Body(JevCall call)
    {
        var cap = JevEndpoints.InputCapChars;
        var questions = new Dictionary<string, JevBatchQuestion>(StringComparer.Ordinal);
        var perQuestion = Math.Max(MinStateChars, cap / 2 / Math.Max(1, call.Questions.Count));
        var spent = 0;
        foreach (var q in call.Questions)
        {
            var instructions = Cut(Scrub(q.Instructions), perQuestion);
            var criteria = q.Criteria?.ToDictionary(static p => p.Key, p => Scrub(p.Value), StringComparer.Ordinal);
            spent += instructions.Length + (criteria?.Sum(static p => p.Key.Length + p.Value.Length + 8) ?? 0);
            questions[q.Name] = q.Type switch
            {
                "choice" => new JevBatchChoice(instructions, criteria ?? []),
                "score" => new JevBatchScore(instructions, [.. (criteria ?? []).Values]),
                _ => new JevBatchNoul(instructions),
            };
        }

        var state = Cut(Scrub(call.Text), Math.Max(MinStateChars, cap - spent - EnvelopeChars));
        return new JevBatchRequest(JsonValue.Create(state), call.Model ?? JevEndpoints.Model, questions);
    }

    private string Scrub(string text) => redactor.Scrub(text).Text;

    private static string Cut(string text, int max)
    {
        if (text.Length <= max)
        {
            return text;
        }

        var keep = max > 0 && char.IsHighSurrogate(text[max - 1]) ? max - 1 : max;
        return string.Create(CultureInfo.InvariantCulture, $"{text[..keep]}[truncated {text.Length - keep} chars]");
    }

    private static Error Fail(string reason) => SindriErrors.GraphBlockFailed.With("jev: " + reason);
}
