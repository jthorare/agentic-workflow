using System.Text.Json.Serialization;
using Aw.Sindri.Core.Sandbox;
using Aw.Sindri.Core.Workflow.Ids;

namespace Aw.Sindri.Core.Workflow.Blocks;

/// <summary>
/// What a block declares about itself (spec §2), exposed as its static <see cref="IDescribedBlock.Descriptor"/>. The
/// validator reads only descriptors; approval pins their hash (SHA-256 over the RFC 8785 canonical JSON), and a
/// behaviour change bumps <paramref name="Version"/>.
/// </summary>
/// <param name="Id">The block's id; its prefix matches <paramref name="Kind"/>.</param>
/// <param name="Kind">The block's kind.</param>
/// <param name="Version">The behaviour version.</param>
/// <param name="Inputs">The facts it reads.</param>
/// <param name="Outputs">The facts it writes, each with the outcomes it is required on; checked against every result and desk report.</param>
/// <param name="Outcomes">The outcomes it reports.</param>
/// <param name="Capabilities">What it may reach.</param>
/// <param name="Parameters">Its 'with:' parameters.</param>
/// <param name="Rung">How a gate decides; null for every other kind.</param>
/// <param name="DeskCapable">A Step only: it may run on the desk runtime (W5, W20).</param>
/// <param name="OutcomesFrom">Null, or the list parameter whose entries name the outcomes (DT spec §8.1): a list parameter ("choices"), or "&lt;stateParam&gt;.&lt;listParam&gt;" for a twin that reads another state's Step ("from.choices"). Written only when set, so a descriptor without it hashes as before.</param>
/// <param name="InputsFrom">Null, or the list parameter whose entries are the fact keys the Step reads, named as <paramref name="OutcomesFrom"/> names its list.</param>
/// <param name="HumanOnly">True when only a human may report this Step's run (DT spec §8.5); written only when true, never false, so a descriptor without it hashes as before.</param>
/// <param name="Runtime">Null, or where the Step runs by default (DT spec §8.2). Written only when set.</param>
/// <param name="LoginSource">Null, or the model login source this Step asks for (sandbox spec SB18, owner note 4): only a stricter one than the profile, repo and target give, never a looser (the launcher takes the stricter). Written only when set, so a descriptor without it hashes as before.</param>
/// <param name="MaxDuration">Null, or the longest a block-runtime Step may run before the engine ends it as step.failed 'timeout' (DT spec §8.2). Written only when set, as a duration string ('2m', '90s').</param>
public sealed record BlockDescriptor(
    BlockId Id,
    BlockKind Kind,
    int Version,
    IReadOnlyList<FactKey> Inputs,
    IReadOnlyList<OutputSpec> Outputs,
    IReadOnlyList<OutcomeName> Outcomes,
    Capability Capabilities,
    IReadOnlyList<ParameterSpec> Parameters,
    Rung? Rung,
    bool DeskCapable,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] string? OutcomesFrom = null,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] string? InputsFrom = null,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] bool? HumanOnly = null,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] BlockRuntime? Runtime = null,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull), JsonConverter(typeof(BlockDurationJsonConverter))] TimeSpan? MaxDuration = null,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] ModelLoginSource? LoginSource = null);
