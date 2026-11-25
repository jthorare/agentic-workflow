using System.Text.Json.Serialization;

namespace Aw.Sindri.Core.Setup;

/// <summary>The steps of "set up Sindri once", written in lower case. The order they are shown in is fixed by <see cref="SetupDeriver"/> (first-run spec §3.1), not by this enum.</summary>
[JsonConverter(typeof(JsonStringEnumConverter<SetupStepId>))]
public enum SetupStepId
{
    /// <summary>At least one repo is added.</summary>
    [JsonStringEnumMemberName("repo")]
    Repo,

    /// <summary>The profile is approved.</summary>
    [JsonStringEnumMemberName("profile")]
    Profile,

    /// <summary>The agent is told where Sindri is, for each repo.</summary>
    [JsonStringEnumMemberName("mcp")]
    Mcp,

    /// <summary>The context map is built.</summary>
    [JsonStringEnumMemberName("map")]
    Map,

    /// <summary>The index sources the profile names have their tokens.</summary>
    [JsonStringEnumMemberName("sources")]
    Sources,

    /// <summary>The running Sindri is the installed build (first-run spec §3.1, step 1).</summary>
    [JsonStringEnumMemberName("serve")]
    Serve,

    /// <summary>The agent workflows run on is chosen (step 5).</summary>
    [JsonStringEnumMemberName("agent")]
    Agent,

    /// <summary>The hello-sindri workflow has run once, or the owner dismissed the step (step 8).</summary>
    [JsonStringEnumMemberName("first-workflow")]
    FirstWorkflow,
}
