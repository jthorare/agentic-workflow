using Aw.Kernel;
using Aw.Sindri.Core;
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

    internal FakeFolders Folders { get; } = new();

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

        public ValueTask<Result<Toolkit?>> Find(string name, CancellationToken cancellationToken) => ValueTask.FromResult(Results.Success(Items.GetValueOrDefault(name)));

        public ValueTask<Result<IReadOnlyList<Toolkit>>> List(CancellationToken cancellationToken) => ValueTask.FromResult(Results.Success<IReadOnlyList<Toolkit>>([.. Items.Values.OrderBy(static t => t.Name, StringComparer.Ordinal)]));

        public ValueTask<Result<Unit>> Save(Toolkit toolkit, CancellationToken cancellationToken)
        {
            Items[toolkit.Name] = toolkit;
            return ValueTask.FromResult<Result<Unit>>(default(Unit));
        }
    }

    internal sealed class FakeWorkspace : IToolkitWorkspace
    {
        internal string Hash { get; set; } = "hash-1";

        public ValueTask<Result<string>> SourceHash(string dir, CancellationToken cancellationToken) => ValueTask.FromResult<Result<string>>(Hash);

        public Result<string> BinaryOf(string dir) => dir + "/bin/" + Path.GetFileName(dir);

        public Result<string> TestsProjectOf(string dir) => "Triage.Toolkit.Tests/Triage.Toolkit.Tests.csproj";
    }

    internal sealed class FakeCommands : IToolkitCommands
    {
        /// <summary>Exit codes by the command's second word ('build' or 'test'); a missing key is 0.</summary>
        internal Dictionary<string, int> Exits { get; } = new(StringComparer.Ordinal);

        /// <summary>What a command prints, by its second word; 'ok' when a key is missing, and the valid manifest for '--manifest'.</summary>
        internal Dictionary<string, string> Outputs { get; } = new(StringComparer.Ordinal) { ["--manifest"] = Manifest };

        /// <summary>What a command prints on standard error, by its second word; empty when a key is missing.</summary>
        internal Dictionary<string, string> Errors { get; } = new(StringComparer.Ordinal);

        internal List<string> Ran { get; } = [];

        public ValueTask<ToolkitCommandResult> Run(string workingDir, IReadOnlyList<string> argv, CancellationToken cancellationToken)
        {
            Ran.Add(string.Join(' ', argv));
            var exit = Exits.GetValueOrDefault(argv[1]);
            return ValueTask.FromResult(new ToolkitCommandResult(exit, exit == 0 ? Outputs.GetValueOrDefault(argv[1], "ok") : "failed: " + argv[1], Errors.GetValueOrDefault(argv[1], string.Empty)));
        }
    }

    internal sealed class FakeRuntime : IToolkitRuntime
    {
        internal List<string> Started { get; } = [];

        internal List<string> Stopped { get; } = [];

        internal List<string> Paused { get; } = [];

        internal List<string> Resumed { get; } = [];

        /// <summary>Names that have no process: pause and resume refuse them with SND-TK-001.</summary>
        internal HashSet<string> NotRunning { get; } = new(StringComparer.Ordinal);

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

        public ValueTask<Result<Unit>> Pause(string name, CancellationToken cancellationToken)
        {
            if (NotRunning.Contains(name))
            {
                return ValueTask.FromResult<Result<Unit>>(SindriErrors.ToolkitNotBuilt.With($"'{name}' isn't running"));
            }

            Paused.Add(name);
            return ValueTask.FromResult<Result<Unit>>(default(Unit));
        }

        public ValueTask<Result<Unit>> Resume(string name, CancellationToken cancellationToken)
        {
            if (NotRunning.Contains(name))
            {
                return ValueTask.FromResult<Result<Unit>>(SindriErrors.ToolkitNotBuilt.With($"'{name}' isn't running"));
            }

            Resumed.Add(name);
            return ValueTask.FromResult<Result<Unit>>(default(Unit));
        }
    }

    internal sealed class FakeFolders : IToolkitFolders
    {
        /// <summary>The folders already on disk.</summary>
        internal List<ToolkitFolder> Existing { get; } = [];

        /// <summary>What the generator was asked for: name, first tool, needs.</summary>
        internal List<(string Name, string Tool, string? Needs)> Generated { get; } = [];

        /// <summary>When set, the generator fails with it.</summary>
        internal Error? Failure { get; set; }

        public IReadOnlyList<ToolkitFolder> Folders() => Existing;

        public ValueTask<Result<ToolkitFolder>> Generate(string name, string tool, string? needs, CancellationToken cancellationToken)
        {
            Generated.Add((name, tool, needs));
            return ValueTask.FromResult<Result<ToolkitFolder>>(Failure is { } failure ? failure : new ToolkitFolder(name, "/tk/" + name));
        }
    }
}
