using Aw.Forge.Core.Refs;
using Aw.Forge.Infrastructure.World;
using Aw.Redaction;

namespace Aw.Forge.Testing;

/// <summary>
/// The in-memory forge for tests: the file forge's rules over a world that lives and dies with the instance, a push simulated
/// against its branch tips. It reports <see cref="ForgeKind.File"/>, the kind of the rules it shares; it is never registered by
/// production code.
/// </summary>
internal sealed class FakeForge(ForgeHosts hosts) : WorldForge(ForgeKind.File, hosts, new MemoryWorldStore(), Redactor.Default, git: null);
