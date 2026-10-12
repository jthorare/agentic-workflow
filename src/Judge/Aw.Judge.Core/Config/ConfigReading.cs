using Aw.Judge.Core.Errors;
using Aw.Kernel;

namespace Aw.Judge.Core.Config;

/// <summary>The config a reader acts on: what is stored, or the defaults and why when the stored config is invalid.</summary>
/// <param name="Config">The config to use.</param>
/// <param name="Warning">Why the defaults are used instead of the stored config (AW-JUDGE-007's message), or null.</param>
public sealed record ConfigReading(JudgeConfig Config, string? Warning)
{
    /// <summary>Reads a store's load: AW-JUDGE-007 becomes the defaults with a warning; any other error stays an error.</summary>
    /// <param name="loaded">What IJudgeConfigStore.Load returned.</param>
    /// <returns>The reading, or the store's other error.</returns>
    public static Result<ConfigReading> From(Result<JudgeConfig> loaded)
    {
        ArgumentNullException.ThrowIfNull(loaded);
        if (loaded.IsSuccess)
        {
            return new ConfigReading(loaded.Value, Warning: null);
        }

        return loaded.Error.Code == JudgeErrors.ConfigMalformed.Code
            ? new ConfigReading(JudgeConfig.Default, loaded.Error.Message)
            : loaded.Error;
    }
}
