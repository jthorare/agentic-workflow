using Aw.Sindri.Host.Server;

namespace Aw.Sindri.Tests.Server;

public sealed partial class TodoNoteWatchTests
{
    private sealed class Run(TodoNoteWatch watch, SteppedClock clock, FakeSync sync) : IAsyncDisposable
    {
        /// <summary>Gets the clock the loop waits on.</summary>
        internal SteppedClock Clock => clock;

        /// <summary>Runs <paramref name="act"/>, steps the clock and waits for the loop to arm its next wait.</summary>
        internal async Task Next(TimeSpan by, Func<Task>? act = null, bool expectPass = true)
        {
            var before = sync.Passes;
            if (act is not null)
            {
                await act();
            }

            await clock.Step(by);
            if (expectPass)
            {
                await clock.Armed(() => sync.Passes > before);
                await clock.Armed();
            }
        }

        internal Task Next(TimeSpan by, Action act) => Next(by, () =>
        {
            act();
            return Task.CompletedTask;
        });

        public async ValueTask DisposeAsync()
        {
            await watch.StopAsync(Token);
            watch.Dispose();
        }
    }
}
