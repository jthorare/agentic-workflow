using System.Text.Json.Serialization;

namespace Aw.Sindri.Core.Projects.Vocabulary;

/// <summary>A project's state (projects page spec §3): the six words a Linear status type and an owner's choice share; written in camel case ('inProgress').</summary>
[JsonConverter(typeof(JsonStringEnumConverter<ProjectState>))]
public enum ProjectState
{
    /// <summary>Not planned yet.</summary>
    [JsonStringEnumMemberName("backlog")]
    Backlog,

    /// <summary>Planned, not started.</summary>
    [JsonStringEnumMemberName("planned")]
    Planned,

    /// <summary>Being worked on.</summary>
    [JsonStringEnumMemberName("inProgress")]
    InProgress,

    /// <summary>Put on hold.</summary>
    [JsonStringEnumMemberName("paused")]
    Paused,

    /// <summary>Finished.</summary>
    [JsonStringEnumMemberName("done")]
    Done,

    /// <summary>Dropped.</summary>
    [JsonStringEnumMemberName("canceled")]
    Canceled,
}
