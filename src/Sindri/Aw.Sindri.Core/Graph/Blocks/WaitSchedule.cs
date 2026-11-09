using System.Text.Json.Nodes;
using Aw.Kernel;

namespace Aw.Sindri.Core.Graph.Blocks;

/// <summary>When a 'wait' node wakes: a duration after now, or an 'until' time read in a timezone.</summary>
public static class WaitSchedule
{
    /// <summary>The instant the wait ends.</summary>
    /// <param name="config">The node's config: 'duration', or 'until' with an optional 'timezone'.</param>
    /// <param name="now">The instant the wait starts.</param>
    /// <returns>The wake-up instant, or why the config can't be read.</returns>
    public static Result<DateTimeOffset> WakeAt(JsonObject config, DateTimeOffset now) => throw new NotSupportedException();

    /// <summary>Problems a config has whatever the clock says; references ('{{...}}') are not checked because they have no value yet.</summary>
    /// <param name="config">The node's config.</param>
    /// <returns>One problem per field: the field's name and what to change.</returns>
    public static IReadOnlyList<(string Field, string Problem)> Check(JsonObject config) => throw new NotSupportedException();
}
