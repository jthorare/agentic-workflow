using Aw.Kernel.Testing;
using Aw.Sindri.Core.Commands;
using Aw.Sindri.Core.Ids;
using Aw.Sindri.Core.Workflow.Engine;

namespace Aw.Sindri.Testing;

/// <summary>Builds a <see cref="CallerContext"/> the way the Host would; ids come from a <see cref="SequentialIdGenerator"/>.</summary>
public sealed class CallerContextBuilder
{
    private CallerClass _class = CallerClass.Session;
    private bool _terminal;
    private ClientLabel _client = ClientLabel.None;
    private RepoName? _repo;
    private string? _service;
    private LaunchId? _launch;

    /// <summary>A human caller (no terminal unless <see cref="AtTerminal"/>).</summary>
    /// <returns>This builder.</returns>
    public CallerContextBuilder Human()
    {
        _class = CallerClass.Human;
        return this;
    }

    /// <summary>A human at an interactive terminal.</summary>
    /// <returns>This builder.</returns>
    public CallerContextBuilder AtTerminal()
    {
        _class = CallerClass.Human;
        _terminal = true;
        return this;
    }

    /// <summary>Labels the client.</summary>
    /// <param name="client">The label.</param>
    /// <returns>This builder.</returns>
    public CallerContextBuilder Client(ClientLabel client)
    {
        _client = client;
        return this;
    }

    /// <summary>A session caller (the default).</summary>
    /// <returns>This builder.</returns>
    public CallerContextBuilder Session()
    {
        _class = CallerClass.Session;
        _terminal = false;
        return this;
    }

    /// <summary>A session Sindri itself launched (its X-Sindri-Launch).</summary>
    /// <param name="launch">The launch id.</param>
    /// <returns>This builder.</returns>
    public CallerContextBuilder Launched(string launch = "LCH-00000000000000000000000000000001")
    {
        _class = CallerClass.Session;
        _terminal = false;
        _launch = LaunchId.From(launch);
        return this;
    }

    /// <summary>An in-process 'service:&lt;name&gt;' caller.</summary>
    /// <param name="name">The service.</param>
    /// <returns>This builder.</returns>
    public CallerContextBuilder Service(string name)
    {
        _class = CallerClass.Service;
        _service = name;
        return this;
    }

    /// <summary>Scopes the caller to <paramref name="name"/>.</summary>
    /// <param name="name">The repo name.</param>
    /// <returns>This builder.</returns>
    public CallerContextBuilder InRepo(string name)
    {
        _repo = RepoName.From(name);
        return this;
    }

    /// <summary>Builds the context.</summary>
    /// <returns>The context.</returns>
    public CallerContext Build()
    {
        var ids = new SequentialIdGenerator();
        return _class == CallerClass.Service
            ? CallerContext.Service(_service ?? "host", ids) with { Repo = _repo }
            : CallerContext.Http(new HttpCaller(_class, _terminal, _client, _repo, _launch), ids);
    }
}
