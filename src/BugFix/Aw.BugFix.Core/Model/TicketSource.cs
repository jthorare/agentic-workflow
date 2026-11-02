using System.Text.Json.Serialization;

namespace Aw.BugFix.Core.Model;

/// <summary>Where the ticket came from.</summary>
[JsonConverter(typeof(JsonStringEnumConverter<TicketSource>))]
public enum TicketSource
{
    /// <summary>linear.</summary>
    [JsonStringEnumMemberName("linear")]
    Linear = 1,

    /// <summary>text.</summary>
    [JsonStringEnumMemberName("text")]
    Text = 2,

    /// <summary>github: a GitHub issue; the ticket's id is its number ("#1189", "1189", "owner/repo#1189" or the issue's URL).</summary>
    [JsonStringEnumMemberName("github")]
    Github = 3,
}
