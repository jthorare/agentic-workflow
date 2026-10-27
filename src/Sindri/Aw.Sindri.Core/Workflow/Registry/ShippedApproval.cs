using System.Text.Json.Serialization;

namespace Aw.Sindri.Core.Workflow.Registry;

/// <summary>How a waiting shipped version is approved (#1042): by approve-all with the rest of the hold, or on its own.</summary>
[JsonConverter(typeof(JsonStringEnumConverter<ShippedApproval>))]
public enum ShippedApproval
{
    /// <summary>Approve-all takes it: it gains no capability.</summary>
    [JsonStringEnumMemberName("approveAll")]
    ApproveAll = 1,

    /// <summary>It waits for its own typed 'approve id@hash': it gains a capability, or a recipe it uses does.</summary>
    [JsonStringEnumMemberName("own")]
    Own = 2,
}
