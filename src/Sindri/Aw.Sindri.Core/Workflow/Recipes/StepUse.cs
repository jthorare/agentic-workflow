using System.Text.Json.Serialization;
using Aw.Sindri.Core.Workflow.Ids;

namespace Aw.Sindri.Core.Workflow.Recipes;

/// <summary>The Step an active state runs (spec §3.4): '{ use, with?, inputs? }'; the short form is the bare id.</summary>
/// <param name="Use">The Step block.</param>
/// <param name="With">Its arguments by parameter name.</param>
/// <param name="Inputs">The 'input.…' blocks that fill the Step's injection plan before it starts, in order.</param>
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record StepUse(BlockId Use, IReadOnlyDictionary<string, WithValue> With, IReadOnlyList<BlockUse> Inputs);
