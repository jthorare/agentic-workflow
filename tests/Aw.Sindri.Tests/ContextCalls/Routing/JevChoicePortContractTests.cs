using Aw.Context.Core.Models;
using Aw.Context.Testing.Engine;

namespace Aw.Sindri.Tests.ContextCalls.Routing;

[Trait("Lane", "unit")]
public sealed class JevChoicePortContractTests : ChoicePortContract<IChoicePort>
{
    protected override IChoicePort Create() => new ChoiceRig(ScriptedBatches.Picking()).Port;
}
