using System.Text.Json.Serialization;

namespace Aw.Sindri.Ui.Api.Context;

/// <summary>How much a concept holds (the concept band, proposed contract).</summary>
/// <param name="Features">Features.</param>
/// <param name="Records">Records (tickets, documents).</param>
/// <param name="Decisions">Decisions.</param>
/// <param name="Code">Code files.</param>
internal sealed record ConceptSize(int Features, int Records, int Decisions, int Code)
{
    /// <summary>Everything the concept holds.</summary>
    [JsonIgnore]
    public int Total => Features + Records + Decisions + Code;
}
