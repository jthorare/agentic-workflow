using System.Text;
using System.Text.Json;
using Aw.Context.Core.Lenses;
using Aw.Kernel.Json;
using Aw.Sindri.Features.ContextCalls;
using ModelContextProtocol.Protocol;

namespace Aw.Sindri.Host.Mcp;

/// <summary>
/// Caps a context_lenses success payload at <see cref="Budget"/> characters of JSON, measured with the same
/// <see cref="AwJson.Serialize"/> path <see cref="McpResults"/> writes. A view that already fits is returned unchanged.
/// A longer one is halved and re-serialized until it fits, or until a pass removes nothing.
/// </summary>
internal static class LensPayload
{
    /// <summary>The most characters of JSON context_lenses returns over MCP.</summary>
    internal const int Budget = 48_000;

    /// <summary>The MCP success result: the fitted view, and the usual summary plus a note when that view was cut down.</summary>
    /// <param name="services">The request's services.</param>
    /// <param name="answer">The lenses answer (one anchor, or several).</param>
    /// <returns>The tool result.</returns>
    internal static CallToolResult Deliver(IServiceProvider services, LensesAnswer answer)
    {
        var options = services.GetRequiredService<JsonSerializerOptions>();
        if (answer.Many is { } many)
        {
            var (view, truncated) = Fit(many, options);
            return McpResults.Success(services, view, Text(McpSummaries.LensesSet(many), truncated));
        }

        var single = answer.One!;   // why: exactly one of One and Many is set
        var (one, cut) = Fit(single, options);
        return McpResults.Success(services, one, Text(McpSummaries.Lenses(single), cut));
    }

    /// <summary>The lenses, cut down when their JSON exceeds <see cref="Budget"/>.</summary>
    /// <param name="view">The lenses.</param>
    /// <param name="options">The host's JSON options.</param>
    /// <returns>The view to return, and whether it is smaller than <paramref name="view"/>.</returns>
    internal static (LensesView View, bool Truncated) Fit(LensesView view, JsonSerializerOptions options) =>
        Fit(view, options, ShrinkView);

    /// <summary>The lenses of several anchors, cut down when their JSON exceeds <see cref="Budget"/>.</summary>
    /// <param name="view">The lenses.</param>
    /// <param name="options">The host's JSON options.</param>
    /// <returns>The view to return, and whether it is smaller than <paramref name="view"/>.</returns>
    internal static (LensesSetView View, bool Truncated) Fit(LensesSetView view, JsonSerializerOptions options) =>
        Fit(view, options, ShrinkSet);

    private static (T View, bool Truncated) Fit<T>(T view, JsonSerializerOptions options, Func<T, T?> shrink)
        where T : class
    {
        if (Length(view, options) <= Budget)
        {
            return (view, false);
        }

        var current = view;
        var truncated = false;
        while (shrink(current) is { } smaller)
        {
            current = smaller;
            truncated = true;
            if (Length(current, options) <= Budget)
            {
                break;
            }
        }

        return (current, truncated);
    }

    private static LensesView? ShrinkView(LensesView view)
    {
        var next = Cut(view.Edges, view.Bands, view.Conflicts);
        return Unchanged(view.Edges, view.Bands, view.Conflicts, next)
            ? null
            : view with { Edges = next.Edges, Bands = next.Bands, Conflicts = next.Conflicts };
    }

    private static LensesSetView? ShrinkSet(LensesSetView view)
    {
        var next = Cut(view.Edges, view.Bands, view.Conflicts);
        return Unchanged(view.Edges, view.Bands, view.Conflicts, next)
            ? null
            : view with { Edges = next.Edges, Bands = next.Bands, Conflicts = next.Conflicts };
    }

    /// <summary>Halve the band lists, or the conflicts once those lists are empty.</summary>
    private static (IReadOnlyList<LensEdge> Edges, LensBands Bands, IReadOnlyList<LensConflict> Conflicts) Cut(
        IReadOnlyList<LensEdge> edges, LensBands bands, IReadOnlyList<LensConflict> conflicts) =>
        Primary(edges, bands) > 0
            ? (Half(edges), Halve(bands), conflicts)
            : (edges, bands, Half(conflicts));

    private static bool Unchanged(
        IReadOnlyList<LensEdge> edges,
        LensBands bands,
        IReadOnlyList<LensConflict> conflicts,
        (IReadOnlyList<LensEdge> Edges, LensBands Bands, IReadOnlyList<LensConflict> Conflicts) next) =>
        Size(edges, bands, conflicts) == Size(next.Edges, next.Bands, next.Conflicts);

    private static int Size(IReadOnlyList<LensEdge> edges, LensBands bands, IReadOnlyList<LensConflict> conflicts) =>
        Primary(edges, bands) + conflicts.Count;

    private static int Primary(IReadOnlyList<LensEdge> edges, LensBands bands)
    {
        var product = bands.Product;
        var intent = bands.Intent;
        return edges.Count + product.Own.Count + product.Neighbors.Count + product.Affects.Count + product.Edges.Count
            + intent.Records.Count + intent.Decisions.Count + bands.Code.Files.Count;
    }

    private static LensBands Halve(LensBands bands)
    {
        var product = bands.Product;
        var intent = bands.Intent;
        var code = bands.Code;
        return bands with
        {
            Product = product with
            {
                Own = Half(product.Own),
                Neighbors = Half(product.Neighbors),
                Affects = Half(product.Affects),
                Edges = Half(product.Edges),
            },
            Intent = intent with
            {
                Records = Half(intent.Records),
                Decisions = Half(intent.Decisions),
            },
            Code = code with { Files = Half(code.Files) },
        };
    }

    /// <summary>The first half of the list (its count divided by 2). A half of 0 drops the list to empty, so one item is removed; two or more keep at least one.</summary>
    private static IReadOnlyList<T> Half<T>(IReadOnlyList<T> items)
    {
        var keep = items.Count / 2;
        if (keep == 0)
        {
            return items.Count == 0 ? items : [];
        }

        var kept = new T[keep];
        for (var index = 0; index < keep; index++)
        {
            kept[index] = items[index];
        }

        return kept;
    }

    private static string Text(string summary, bool truncated) =>
        truncated ? summary + "\ntruncated to fit" : summary;

    private static int Length<T>(T value, JsonSerializerOptions options) =>
        Encoding.UTF8.GetCharCount(AwJson.Serialize(value, options));
}
