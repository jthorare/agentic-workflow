using System.Text.Json.Serialization;

namespace Aw.Sindri.Core.Setup;

/// <summary>The steps of "set up Sindri once", in the order they are shown; written in lower case.</summary>
[JsonConverter(typeof(JsonStringEnumConverter<SetupStepId>))]
public enum SetupStepId
{
    /// <summary>At least one repo is added.</summary>
    [JsonStringEnumMemberName("repo")]
    Repo,

    /// <summary>The profile is approved.</summary>
    [JsonStringEnumMemberName("profile")]
    Profile,

    /// <summary>The workflow recipes are approved.</summary>
    [JsonStringEnumMemberName("recipes")]
    Recipes,

    /// <summary>The agent is told where Sindri is, for each repo.</summary>
    [JsonStringEnumMemberName("mcp")]
    Mcp,

    /// <summary>The context map is built.</summary>
    [JsonStringEnumMemberName("map")]
    Map,

    /// <summary>The managed Onyx is up.</summary>
    [JsonStringEnumMemberName("onyx")]
    Onyx,

    /// <summary>The index sources the profile names have their tokens.</summary>
    [JsonStringEnumMemberName("sources")]
    Sources,
}
