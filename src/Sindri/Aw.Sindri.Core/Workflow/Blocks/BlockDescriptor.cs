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
    bool DeskCapable);
