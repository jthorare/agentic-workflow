using System.Text.Json.Serialization;

namespace Aw.Sindri.Ui.Api.Workflow;

/// <summary>The kind of a fact. A kind this build doesn't know reads as <see cref="Payload"/> (API rev 8), so a new one still shows.</summary>
[JsonConverter(typeof(FactTypeJsonConverter))]
internal enum FactType
{
    /// <summary>A line of text.</summary>
    Text = 1,

    /// <summary>A file.</summary>
    File = 2,

    /// <summary>A tracked item.</summary>
    Item = 3,

    /// <summary>A link.</summary>
    Link = 4,

    /// <summary>A scope map.</summary>
    ScopeMap = 5,

    /// <summary>A packet.</summary>
    Packet = 6,

    /// <summary>An evidence set.</summary>
    Evidence = 7,

    /// <summary>A decision.</summary>
    Decision = 8,

    /// <summary>A structured payload; also what an unknown kind reads as.</summary>
    Payload = 9,

    /// <summary>A list of rule findings, drawn as rows.</summary>
    Findings = 10,
}
