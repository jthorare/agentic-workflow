using System.Text.Json;
using Aw.Kernel;
using Aw.Sindri.Core;
using Aw.Sindri.Core.Handoff;

namespace Aw.Sindri.Infrastructure.Handoff;

/// <summary>
/// Reads and writes the bodies of schemas/handoff/inbox.openapi.yaml by hand, so a bad answer is SND-CONN-201 naming the field (never its text: the inbox's
/// text is untrusted). Strict like the schema: a required field that is missing, null or of another type is a violation; fields the schema does not name are ignored.
/// </summary>
internal static class InboxWire
{
    internal static Result<OfferPage> ParsePage(string json)
    {
        var opened = Open(json);
        if (opened.IsError)
        {
            return opened.Error;
        }

        using var document = opened.Value;
        var root = document.RootElement;
        if (root.ValueKind != JsonValueKind.Object)
        {
            return Violation("the page");
        }

        if (!root.TryGetProperty("offers", out var offers) || offers.ValueKind != JsonValueKind.Array)
        {
            return Violation("offers");
        }

        if (!root.TryGetProperty("nextPageToken", out var token) || token.ValueKind is not (JsonValueKind.String or JsonValueKind.Null))
        {
            return Violation("nextPageToken");
        }

        var list = new List<Offer>();
        foreach (var element in offers.EnumerateArray())
        {
            var offer = ReadOffer(element, "offers[]");
            if (offer.IsError)
            {
                return offer.Error;
            }

            list.Add(offer.Value);
        }

        return new OfferPage(list, token.GetString());
    }

    internal static Result<ClaimResult> ParseClaim(string json)
    {
        var opened = Open(json);
        if (opened.IsError)
        {
            return opened.Error;
        }

        using var document = opened.Value;
        var root = document.RootElement;
        if (root.ValueKind != JsonValueKind.Object)
        {
            return Violation("the claim");
        }

        if (Required(root, "claimId") is not { } claim)
        {
            return Violation("claimId");
        }

        if (!root.TryGetProperty("offer", out var offerElement))
        {
            return Violation("offer");
        }

        var offer = ReadOffer(offerElement, "offer");
        return offer.IsError ? offer.Error : new ClaimResult(claim, offer.Value);
    }

    internal static Result<Offer> ReadOffer(JsonElement element, string where)
    {
        if (element.ValueKind != JsonValueKind.Object)
        {
            return Violation(where);
        }

        var head = ReadHead(element, where);
        if (head.IsError)
        {
            return head.Error;
        }

        var tail = ReadTail(element, where);
        return tail.IsError
            ? tail.Error
            : new Offer(head.Value.Key, head.Value.Title, head.Value.Repo, tail.Value.Branch, tail.Value.Prompt, tail.Value.Links, tail.Value.Labels);
    }

    internal static byte[] ClaimBody(string owner) => Write(w =>
    {
        w.WriteStartObject();
        w.WriteString("owner", owner);
        w.WriteEndObject();
    });

    internal static byte[] ResultBody(InboxResult result) => Write(w =>
    {
        w.WriteStartObject();
        w.WriteString("claimId", result.ClaimKey);
        w.WriteBoolean("succeeded", result.Succeeded);
        w.WriteString("markdown", result.Markdown);
        w.WriteStartArray("links");
        foreach (var link in result.Links)
        {
            w.WriteStartObject();
            w.WriteString("label", link.Label);
            w.WriteString("url", link.Url);
            w.WriteEndObject();
        }

        w.WriteEndArray();
        w.WriteEndObject();
    });

    private static Result<OfferHead> ReadHead(JsonElement element, string where)
    {
        if (Required(element, "id") is not { } key)
        {
            return Violation(where + ".id");
        }

        if (Required(element, "title") is not { } title)
        {
            return Violation(where + ".title");
        }

        return Required(element, "repo") is { } repo ? new OfferHead(key, title, repo) : Violation(where + ".repo");
    }

    private static Result<OfferTail> ReadTail(JsonElement element, string where)
    {
        if (!Optional(element, "branch", out var branch))
        {
            return Violation(where + ".branch");
        }

        if (!Optional(element, "prompt", out var prompt))
        {
            return Violation(where + ".prompt");
        }

        var links = ReadLinks(element, where + ".links");
        if (links.IsError)
        {
            return links.Error;
        }

        var labels = ReadLabels(element, where + ".labels");
        return labels.IsError ? labels.Error : new OfferTail(branch, prompt, links.Value, labels.Value);
    }

    private static Result<IReadOnlyList<HandoffLink>> ReadLinks(JsonElement element, string where)
    {
        if (!element.TryGetProperty("links", out var array) || array.ValueKind != JsonValueKind.Array)
        {
            return Violation(where);
        }

        var links = new List<HandoffLink>();
        foreach (var link in array.EnumerateArray())
        {
            if (link.ValueKind != JsonValueKind.Object || Required(link, "label") is not { } label)
            {
                return Violation(where + "[].label");
            }

            if (Required(link, "url") is not { } url || !IsWebUrl(url))
            {
                return Violation(where + "[].url");
            }

            links.Add(new HandoffLink(label, url));
        }

        return links;
    }

    private static Result<IReadOnlyList<string>> ReadLabels(JsonElement element, string where)
    {
        if (!element.TryGetProperty("labels", out var array) || array.ValueKind != JsonValueKind.Array)
        {
            return Violation(where);
        }

        var labels = new List<string>();
        foreach (var label in array.EnumerateArray())
        {
            if (label.ValueKind != JsonValueKind.String || label.GetString() is not { Length: > 0 } text)
            {
                return Violation(where + "[]");
            }

            labels.Add(text);
        }

        return labels;
    }

    private static bool IsWebUrl(string url) =>
        url.StartsWith("http://", StringComparison.OrdinalIgnoreCase) || url.StartsWith("https://", StringComparison.OrdinalIgnoreCase);

    // A required string: present, a JSON string and not empty. Null when it is not.
    private static string? Required(JsonElement element, string name) =>
        element.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String && value.GetString() is { Length: > 0 } text ? text : null;

    // An optional string: absent or null reads as null (true); a value of another type is not allowed (false).
    private static bool Optional(JsonElement element, string name, out string? text)
    {
        text = null;
        if (!element.TryGetProperty(name, out var value) || value.ValueKind == JsonValueKind.Null)
        {
            return true;
        }

        text = value.ValueKind == JsonValueKind.String ? value.GetString() : null;
        return value.ValueKind == JsonValueKind.String;
    }

    private static Result<JsonDocument> Open(string json)
    {
        try
        {
            return JsonDocument.Parse(json);
        }
        catch (JsonException)
        {
            return SindriErrors.InboxContractViolation.With("the answer is not JSON");
        }
    }

    private static Error Violation(string field) => SindriErrors.InboxContractViolation.With($"the inbox's answer has no valid '{field}'");

    private static byte[] Write(Action<Utf8JsonWriter> body)
    {
        using var stream = new MemoryStream();
        using (var writer = new Utf8JsonWriter(stream))
        {
            body(writer);
        }

        return stream.ToArray();
    }

    private sealed record OfferHead(string Key, string Title, string Repo);

    private sealed record OfferTail(string? Branch, string? Prompt, IReadOnlyList<HandoffLink> Links, IReadOnlyList<string> Labels);
}
