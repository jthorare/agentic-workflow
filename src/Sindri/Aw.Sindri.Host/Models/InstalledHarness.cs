using Aw.Context.Core.Engine;
using Aw.Judge.Core.Ports;

namespace Aw.Sindri.Host.Models;

/// <summary>An agent CLI that is on PATH, with the provider that runs it.</summary>
/// <param name="Harness">The harness.</param>
/// <param name="Provider">The provider the judge's lineup gave it.</param>
internal sealed record InstalledHarness(ModelHarness Harness, IModelProvider Provider);
