using Aw.Sindri.Core.Messages;

namespace Aw.Sindri.Infrastructure.Messages;

/// <summary>What <see cref="SlackWebClient"/> reads from the box (messages spec §8.1): AW_SLACK_BASE_URL and the Sindri home that may hold the fixture marker.</summary>
/// <param name="BaseUrlOverride">AW_SLACK_BASE_URL's value, or null. Taken only under <see cref="SlackBaseUrlRule"/>.</param>
/// <param name="Home">The Sindri home ($AW_SINDRI_HOME).</param>
internal sealed record SlackWebSettings(string? BaseUrlOverride, string Home)
{
    /// <summary>The environment variable of the loopback base override (fixture runs only).</summary>
    public const string BaseUrlVariable = "AW_SLACK_BASE_URL";

    // A marker is two short lines; anything bigger is not one.
    private const long MarkerMaxBytes = 256;

    /// <summary>Reads the settings from the environment.</summary>
    /// <param name="environment">The process environment.</param>
    /// <param name="home">The Sindri home.</param>
    /// <returns>The settings.</returns>
    public static SlackWebSettings From(Func<string, string?> environment, string home) => new(environment(BaseUrlVariable), home);

    /// <summary>The marker file's text, or null when the home has none (or it can't be read). Read on each call, so 'slack-fixture.sh down' takes effect at once.</summary>
    /// <returns>The marker's text, or null.</returns>
    public string? Marker()
    {
        if (BaseUrlOverride is null)
        {
            return null;   // why: without the variable the marker can't change anything, so the file is not touched
        }

        try
        {
            var file = new FileInfo(Path.Combine(Home, SlackBaseUrlRule.MarkerFile));
            return file.Exists && file.Length <= MarkerMaxBytes ? File.ReadAllText(file.FullName) : null;
        }
        catch (IOException)
        {
            return null;
        }
        catch (UnauthorizedAccessException)
        {
            return null;
        }
    }
}
