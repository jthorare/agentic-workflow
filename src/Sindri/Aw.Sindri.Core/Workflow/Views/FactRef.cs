using System.Text.Json.Serialization;
using Aw.Sindri.Core.Launch;

namespace Aw.Sindri.Core.Workflow.Views;

/// <summary>The per-type handle of a fact: a file's name, size, hash and path; an item's id; a link's url, host and title; a fact key for the viewers that fetch the body.</summary>
/// <param name="Name">File name.</param>
/// <param name="Size">File size.</param>
/// <param name="Hash">File hash.</param>
/// <param name="Path">File path.</param>
/// <param name="ItemId">Item id.</param>
/// <param name="Url">Link url.</param>
/// <param name="Host">Link host.</param>
/// <param name="Title">Link title.</param>
/// <param name="FactKey">Key for GetInstanceFact.</param>
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record FactRef(
    string? Name,
    long? Size,
    string? Hash,
    string? Path,
    ItemKey? ItemId,
    string? Url,
    string? Host,
    string? Title,
    string? FactKey);
