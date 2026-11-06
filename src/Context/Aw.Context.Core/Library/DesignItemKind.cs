using System.Text.Json.Serialization;

namespace Aw.Context.Core.Library;

/// <summary>What a design-doc item is (spec 2026-11-04 §4.2, the id-kinds table).</summary>
[JsonConverter(typeof(StrictEnumConverter<DesignItemKind>))]
public enum DesignItemKind
{
    /// <summary>A heading with no id; its id is its anchor.</summary>
    [JsonStringEnumMemberName("section")]
    Section,

    /// <summary>A phase, <c language="text">Ph&lt;n&gt;</c> with an optional letter or <c language="text">-&lt;n&gt;</c>.</summary>
    [JsonStringEnumMemberName("phase")]
    Phase,

    /// <summary>A requirement, <c language="text">REQ-&lt;n&gt;</c> or <c language="text">REQ-[UESOW]&lt;n&gt;</c>.</summary>
    [JsonStringEnumMemberName("requirement")]
    Requirement,

    /// <summary>An acceptance criterion, <c language="text">AC-&lt;n&gt;</c>.</summary>
    [JsonStringEnumMemberName("acceptance")]
    Acceptance,

    /// <summary>A test-plan row, <c language="text">TP-&lt;n&gt;</c>.</summary>
    [JsonStringEnumMemberName("testPlan")]
    TestPlan,

    /// <summary>A task, <c language="text">TASK-&lt;n&gt;</c>.</summary>
    [JsonStringEnumMemberName("task")]
    Task,
}
