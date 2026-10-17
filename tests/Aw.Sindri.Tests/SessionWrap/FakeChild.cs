using Aw.Sindri.Host.Commands.SessionWrap;

namespace Aw.Sindri.Tests.SessionWrap;

internal sealed class FakeChild : IWrapChild
{
    private readonly TaskCompletionSource<int> _exited = new(TaskCreationOptions.RunContinuationsAsynchronously);

    public Task<int> Exited => _exited.Task;

    public void Exit(int code) => _exited.TrySetResult(code);

    public void Dispose()
    {
    }
}
