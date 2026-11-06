namespace Aw.Sindri.Ui.Api.Workflow;

/// <summary>Reads and writes a <see cref="FactType"/> as its wire word; a word this build doesn't know (or a non-string) reads as payload.</summary>
internal sealed class FactTypeJsonConverter() : WordEnumJsonConverter<FactType>(
    [
        (FactType.Text, "text"), (FactType.File, "file"), (FactType.Item, "item"), (FactType.Link, "link"),
        (FactType.ScopeMap, "scope-map"), (FactType.Packet, "packet"), (FactType.Evidence, "evidence"),
        (FactType.Decision, "decision"), (FactType.Payload, "payload"), (FactType.Findings, "findings"),
    ],
    FactType.Payload);
