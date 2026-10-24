using System.Text.Json.Serialization;
using Aw.Kernel.Ids;
using Aw.Kernel.Json;
using Vogen;

namespace Aw.Sindri.Core.Line;

/// <summary>A phase's id as the plan names it ('P1'): not empty, at most 256 characters. A linked instance's address names its slug (<see cref="Workflow.Engine.InstanceAddress.LinkedId"/>).</summary>
[ValueObject<string>(comparison: ComparisonGeneration.Omit)]
[JsonConverter(typeof(StringIdJsonConverter<LinePhaseId>))]
public readonly partial struct LinePhaseId : ITypedId<LinePhaseId, string>
{
    private static Vogen.Validation Validate(string value) =>
        value.Length > 0 ? Vogen.Validation.Ok : Vogen.Validation.Invalid("A phase id is not empty and at most 256 characters");

    private static string NormalizeInput(string input) => input.Length > 256 ? string.Empty : input;
}
