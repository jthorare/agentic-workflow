using System.Text.Json.Serialization;

namespace Aw.Sindri.Core.Evolve;

/// <summary>What saving a proposal did.</summary>
[JsonConverter(typeof(JsonStringEnumConverter<ProposalSaveOutcome>))]
public enum ProposalSaveOutcome
{
    /// <summary>A new row.</summary>
    Saved,

    /// <summary>An open proposal with the same target and title took its evidence and sources.</summary>
    Merged,

    /// <summary>The owner rejected the same proposal before; nothing was written.</summary>
    PreviouslyRejected,
}
