using Aw.Kernel;
using Aw.Sindri.Core.Ids;
using Aw.Sindri.Core.Jobs;
using Aw.Sindri.Core.Toolkits;
using Aw.Sindri.Testing;
using Aw.Sindri.Testing.Prepare;

namespace Aw.Sindri.Tests.BuildToolkit;

/// <summary>The toolkit slices over in-memory ports: a store, a folder that hashes to a settable value, a command runner that answers per command and a heavy-slot fake. It never runs a real dotnet.</summary>
internal sealed class ToolkitRig : IDisposable
{
    internal const string Manifest = """{"name":"triage","version":"1.0.0","resources":[],"tools":[{"name":"ping","description":"Ping.","inputSchema":{"type":"object"},"outputSchema":{"type":"object"}}]}""";

    private readonly IDisposable _entered;

    internal ToolkitRig()
    {
        Store.Items["triage"] = new Toolkit("triage", "/tk/triage", ToolkitState.Off, SourceHash: null, BuiltAt: null, Manifest: null, CreatedBy: "human");
        _entered = Jobs.Enter(new JobContext(JobId.From("JOB-" + new string('b', 32)), JobKind.From("BuildToolkit"), Progress));
    }

    internal FakeStore Store { get; } = new();

    internal FakeWorkspace Workspace { get; } = new();

    internal FakeCommands Commands { get; } = new();

    internal FakeHeavySlots Slots { get; } = new();

    internal FakeRuntime Runtime { get; } = new();

    internal FakeJobContextAccessor Jobs { get; } = new();

    internal Lines Progress { get; } = new();

    internal static CancellationToken Token => TestContext.Current.CancellationToken;

    public void Dispose() => _entered.Dispose();

    internal sealed class Lines : IProgress<string>
    {
        private readonly List<string> _seen = [];

        internal IReadOnlyList<string> Seen => _seen;

        public void Report(string value) => _seen.Add(value);
    }

    internal sealed class FakeStore : IToolkitStore
    {
        internal Dictionary<string, Toolkit> Items { get; } = new(StringComparer.Ordinal);

        public ValueTask<Toolkit?> Find(string name, CancellationToken cancellationToken) => ValueTask.FromResult(Items.GetValueOrDefault(name));

        public ValueTask<IReadOnlyList<Toolkit>> List(CancellationToken cancellationToken) => ValueTask.FromResult<IReadOnlyList<Toolkit>>([.. Items.Values.OrderBy(static t => t.Name, StringComparer.Ordinal)]);

        public ValueTask Save(Toolkit toolkit, CancellationToken cancellationToken)
        {
            Items[toolkit.Name] = toolkit;
            return ValueTask.CompletedTask;
        }
    }

    internal sealed class FakeWorkspace : IToolkitWorkspace
    {
        internal string Hash { get; set; } = "hash-1";

        public ValueTask<Result<string>> SourceHash(string dir, CancellationToken cancellationToken) => ValueTask.FromResult<Result<string>>(Hash);

        public ValueTask<Result<string>> ReadManifest(string dir, CancellationToken cancellationToken) => ValueTask.FromResult<Result<string>>(Manifest);
    }

    internal sealed class FakeCommands : IToolkitCommands
    {
        /// <summary>Exit codes by the command's second word ('build' or 'test'); a missing key is 0.</summary>
        internal Dictionary<string, int> Exits { get; } = new(StringComparer.Ordinal);

        internal List<string> Ran { get; } = [];

        public ValueTask<ToolkitCommandResult> Run(string workingDir, IReadOnlyList<string> argv, CancellationToken cancellationToken)
        {
            Ran.Add(string.Join(' ', argv));
            var exit = Exits.GetValueOrDefault(argv[1]);
            return ValueTask.FromResult(new ToolkitCommandResult(exit, exit == 0 ? "ok" : "failed: " + argv[1]));
        }
    }

    internal sealed class FakeRuntime : IToolkitRuntime
    {
        internal List<string> Started { get; } = [];

        internal List<string> Stopped { get; } = [];

        public ValueTask<Result<Unit>> Start(Toolkit toolkit, CancellationToken cancellationToken)
        {
            Started.Add(toolkit.Name);
            return ValueTask.FromResult<Result<Unit>>(default(Unit));
        }

        public ValueTask<Result<Unit>> Stop(string name, CancellationToken cancellationToken)
        {
            Stopped.Add(name);
            return ValueTask.FromResult<Result<Unit>>(default(Unit));
        }
    }
}
