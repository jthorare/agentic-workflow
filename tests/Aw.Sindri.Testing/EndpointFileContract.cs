using Aw.Sindri.Core.Control;
using Xunit;

namespace Aw.Sindri.Testing;

/// <summary>The contract every <see cref="IEndpointFile"/> honours, run against the in-memory record and the file.</summary>
public abstract class EndpointFileContract
{
    private static readonly ControlEndpoint _endpoint = new(new Uri("http://127.0.0.1:7410"), 42, 3);

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    /// <summary>An empty record.</summary>
    /// <returns>The record.</returns>
    protected abstract IEndpointFile Create();

    /// <summary>Damages what <paramref name="file"/> holds (garbage where the record was).</summary>
    /// <param name="file">The record from <see cref="Create"/>.</param>
    protected abstract void Damage(IEndpointFile file);

    /// <summary>Nothing is recorded at first; a written record reads back field for field.</summary>
    /// <returns>The test.</returns>
    [Fact]
    public async Task A_written_record_reads_back()
    {
        var file = Create();
        Expect.True((await file.Read(Token)).Value is null);

        _ = await file.Write(_endpoint, Token);

        Expect.Equal(_endpoint, (await file.Read(Token)).Value);
    }

    /// <summary>A damaged record reads as none, so a client says 'not running' instead of failing.</summary>
    /// <returns>The test.</returns>
    [Fact]
    public async Task A_damaged_record_reads_as_none()
    {
        var file = Create();
        _ = await file.Write(_endpoint, Token);

        Damage(file);
        var read = await file.Read(Token);

        Expect.True(read.IsSuccess && read.Value is null);
    }

    /// <summary>Remove deletes the record only for the pid that wrote it: a stopping process never removes its successor's.</summary>
    /// <returns>The test.</returns>
    [Fact]
    public async Task Remove_takes_away_only_the_same_pids_record()
    {
        var file = Create();
        _ = await file.Write(_endpoint, Token);

        _ = await file.Remove(_endpoint.Pid + 1, Token);
        var kept = (await file.Read(Token)).Value;
        _ = await file.Remove(_endpoint.Pid, Token);

        Expect.Equal(_endpoint, kept);
        Expect.True((await file.Read(Token)).Value is null);
    }
}
