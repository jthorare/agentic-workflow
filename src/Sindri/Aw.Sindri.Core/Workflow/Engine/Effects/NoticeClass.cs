using System.Text.Json.Serialization;

namespace Aw.Sindri.Core.Workflow.Engine.Effects;

/// <summary>How a desk notice reaches the human (engine spec §6.3; engine P6 unit (c)).</summary>
[JsonConverter(typeof(JsonStringEnumConverter<NoticeClass>))]
public enum NoticeClass
{
    /// <summary>Parks, escalations, takeoverIdle and an instance's first desk task: shown now, within D §10.1's budget.</summary>
    [JsonStringEnumMemberName("interrupt")]
    Interrupt = 1,

    /// <summary>Later desk tasks: non-interrupting, collected into the daily digest.</summary>
    [JsonStringEnumMemberName("info")]
    Info = 2,
}
