namespace Aw.Sindri.Tests.StartSession;

internal sealed class OpenGate : Core.Commands.ICommandGate
{
    public ValueTask<IDisposable?> Enter(CancellationToken cancellationToken) => ValueTask.FromResult<IDisposable?>(new Hold());

    private sealed class Hold : IDisposable
    {
        public void Dispose()
        {
        }
    }
}
