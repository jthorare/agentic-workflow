using System.Text.Json.Serialization;

namespace Aw.Sindri.Core.Workflow.Views;

/// <summary>The type of a fact row (API spec section 3.8); a type the API doesn't know is served as payload. The wire names are the table in <see cref="FactTypeJsonConverter"/>.</summary>
[JsonConverter(typeof(FactTypeJsonConverter))]
public enum FactType
{
    /// <summary>Typed text.</summary>
    Text = 1,

    /// <summary>A file.</summary>
    File = 2,

    /// <summary>An item.</summary>
    Item = 3,

    /// <summary>A link.</summary>
    Link = 4,

    /// <summary>A scope map.</summary>
    ScopeMap = 5,

    /// <summary>A packet.</summary>
    Packet = 6,

    /// <summary>Evidence packs.</summary>
    Evidence = 7,

    /// <summary>A decision.</summary>
    Decision = 8,

    /// <summary>Anything else, shown as a tree.</summary>
    Payload = 9,

    /// <summary>A list of rule findings (<c language="text">fact:findings</c>), shown as rows.</summary>
    Findings = 10,
}
