using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using Aw.Kernel.Ids;
using Aw.Kernel.Json;
using Vogen;

namespace Aw.Sindri.Core.Launch;

/// <summary>
/// An agent launch request's opaque id (UI spec E-1, E-19; API P10a's 'GET /launches/{launchId}'): 'LCH-' and 32
/// lower-case hex digits. An open-request answers one (<see cref="Workflow.Engine.Triggers.OpenRequested"/>); a start taken over carries it as its causation.
/// </summary>
[ValueObject<string>(comparison: ComparisonGeneration.Omit)]
[JsonConverter(typeof(StringIdJsonConverter<LaunchId>))]
public readonly partial struct LaunchId : ITypedId<LaunchId, string>
{
    /// <summary>The id for <paramref name="value"/>, written as 'LCH-' and its 32 hex digits.</summary>
    /// <param name="value">A fresh id from the id generator.</param>
    /// <returns>The id.</returns>
    public static LaunchId For(Guid value) => From("LCH-" + value.ToString("N"));

    private static Vogen.Validation Validate(string value) =>
        Shape.IsMatch(value) ? Vogen.Validation.Ok : Vogen.Validation.Invalid("A launch id is 'LCH-' and 32 lower-case hex digits");

    [GeneratedRegex("^LCH-[0-9a-f]{32}\\z", RegexOptions.CultureInvariant, matchTimeoutMilliseconds: 1000)]
    private static partial Regex Shape { get; }
}
