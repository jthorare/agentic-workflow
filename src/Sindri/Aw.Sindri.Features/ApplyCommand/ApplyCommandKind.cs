using System.Text.Json.Serialization;

namespace Aw.Sindri.Features.ApplyCommand;

/// <summary>Which command 'ApplyCommand' carries (engine spec §6.3): an answer, a recipe command, a cancel, a takeover or a handback.</summary>
[JsonConverter(typeof(JsonStringEnumConverter<ApplyCommandKind>))]
public enum ApplyCommandKind
{
    /// <summary>'workflow answer', in its variants.</summary>
    [JsonStringEnumMemberName("answer")]
    Answer = 1,

    /// <summary>'workflow command &lt;verb&gt;': the recipe's 'command.&lt;verb&gt;' trigger.</summary>
    [JsonStringEnumMemberName("command")]
    Command = 2,

    /// <summary>'workflow cancel'.</summary>
    [JsonStringEnumMemberName("cancel")]
    Cancel = 3,

    /// <summary>'workflow takeover' (class pause: a session may take over; spec §5.5, §6.2).</summary>
    [JsonStringEnumMemberName("takeover")]
    Takeover = 4,

    /// <summary>'workflow handback' (class desk: a session may not).</summary>
    [JsonStringEnumMemberName("handback")]
    Handback = 5,

    /// <summary>'workflow rewind' (#1270): re-enter an earlier state, keeping the facts.</summary>
    [JsonStringEnumMemberName("rewind")]
    Rewind = 6,
}
