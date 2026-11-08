using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using Aw.Kernel.Ids;
using Aw.Kernel.Json;
using Aw.Sindri.Core.Launch;
using Vogen;

namespace Aw.Sindri.Core.Workflow.Engine;

/// <summary>
/// A trigger's id on an instance (spec §5.6): 'wf:{instance}:{trigger}', which its ledger events carry as the
/// trigger's id. Idempotency is Temporal's (an Update's id is its nonce, §5.2), so no decision is looked up by it. The
/// trigger part is the trigger's own id: 'cmd:{nonce}:{verb}:{sha256 of its arguments}' for a command,
/// 'run:{token}:{sha256 of the report}' for a Step report, 'timer:{entry}:{kind}' for a timer, 'event:{sequence}',
/// 'child:{child event key}', 'recipe.revoked:{sequence}', 'open:{launch}' and so on.
/// At most 512 characters.
/// </summary>
[ValueObject<string>(comparison: ComparisonGeneration.Omit)]
[JsonConverter(typeof(StringIdJsonConverter<TriggerKey>))]
public readonly partial struct TriggerKey : ITypedId<TriggerKey, string>
{
    private const int PartStart = 3 + 36 + 1;

    /// <summary>The instance the trigger is for.</summary>
    /// <returns>The instance in 'wf:{instance}:…'.</returns>
    public WorkflowInstanceId Instance() => WorkflowInstanceId.From(Value[3..(PartStart - 1)]);

    /// <summary>The trigger's own id, after 'wf:{instance}:'.</summary>
    public string Part => Value[PartStart..];

    /// <summary>The key of <paramref name="part"/> on <paramref name="instance"/>.</summary>
    /// <param name="instance">The instance.</param>
    /// <param name="part">The trigger's own id.</param>
    /// <returns>'wf:{instance}:{part}'.</returns>
    public static TriggerKey For(WorkflowInstanceId instance, string part) => From("wf:" + instance.Value + ":" + part);

    private static Vogen.Validation Validate(string value) =>
        Shape.IsMatch(value) ? Vogen.Validation.Ok : Vogen.Validation.Invalid("A trigger key is 'wf:{instance}:{trigger}', at most 512 characters");

    [GeneratedRegex("^wf:WFI-[0-9a-f]{32}:[!-~]+\\z", RegexOptions.CultureInvariant, matchTimeoutMilliseconds: 1000)]
    private static partial Regex Shape { get; }

    private static string NormalizeInput(string input) => input.Length > 512 ? string.Empty : input;
}
