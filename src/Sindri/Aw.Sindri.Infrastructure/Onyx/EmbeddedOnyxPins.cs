using Aw.Sindri.Core.Onyx;

namespace Aw.Sindri.Infrastructure.Onyx;

/// <summary>The pins from context/onyx.lock, embedded in this assembly at build time (resource name onyx.lock).</summary>
internal sealed class EmbeddedOnyxPins : IOnyxPinsSource
{
    private readonly Lazy<OnyxPins> _pins = new(Load);

    /// <inheritdoc />
    public OnyxPins Pins => _pins.Value;

    private static OnyxPins Load()
    {
        using var stream = typeof(EmbeddedOnyxPins).Assembly.GetManifestResourceStream("onyx.lock")
            ?? throw new InvalidOperationException("The onyx.lock resource is missing from Aw.Sindri.Infrastructure.");
        using var reader = new StreamReader(stream);
        var parsed = OnyxPins.Parse(reader.ReadToEnd());
        return parsed.IsSuccess ? parsed.Value : throw new InvalidOperationException($"{parsed.Error.Code}: {parsed.Error.Message}");
    }
}
