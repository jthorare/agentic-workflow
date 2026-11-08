using System.Text.Json.Serialization;
using Aw.Sindri.Core.Json;

namespace Aw.Sindri.Core.NeedsYou;

/// <summary>What a needs-you row is about (API spec §3.6); 'profile' was added by the owner's decision of 2026-10-14 (P4 plan G8).</summary>
[JsonConverter(typeof(StrictEnumConverter<SubjectKind>))]
public enum SubjectKind
{
    /// <summary>A work item.</summary>
    [JsonStringEnumMemberName("item")]
    Item,

    /// <summary>A decision.</summary>
    [JsonStringEnumMemberName("decision")]
    Decision,

    /// <summary>A workflow instance.</summary>
    [JsonStringEnumMemberName("instance")]
    Instance,

    /// <summary>A job.</summary>
    [JsonStringEnumMemberName("job")]
    Job,

    /// <summary>A background service.</summary>
    [JsonStringEnumMemberName("service")]
    Service,

    /// <summary>The profile; the id is the live profile's hash.</summary>
    [JsonStringEnumMemberName("profile")]
    Profile,

    /// <summary>A setup step; the id is the step ('repo', 'recipes' or 'mcp').</summary>
    [JsonStringEnumMemberName("setup")]
    Setup,

    /// <summary>A workflow recipe; the id is the recipe's id.</summary>
    [JsonStringEnumMemberName("recipe")]
    Recipe,

    /// <summary>A graph run, named by its run id.</summary>
    [JsonStringEnumMemberName("workflow-run")]
    WorkflowRun,
}
