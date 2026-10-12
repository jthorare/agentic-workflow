using System.Text.Json;
using Aw.Sindri.Core.Workflow.Ids;

namespace Aw.Sindri.Core.Workflow.Blocks;

/// <summary>What a Step starts with (spec §2). The engine (P3, P4) fills it; later phases add the run token and the frame.</summary>
/// <param name="Step">The Step's id.</param>
/// <param name="Arguments">Its 'with:' arguments, resolved to values.</param>
/// <param name="Facts">The instance's facts.</param>
/// <param name="Injection">The sections its Step inputs filled.</param>
public sealed record StepContext(
    BlockId Step,
    IReadOnlyDictionary<string, JsonElement> Arguments,
    IReadOnlyDictionary<FactKey, JsonElement> Facts,
    InjectionSections Injection);
