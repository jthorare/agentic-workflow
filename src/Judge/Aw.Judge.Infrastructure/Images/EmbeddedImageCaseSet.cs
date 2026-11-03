using Aw.Judge.Core.Images;
using Aw.Judge.Core.Ports;

namespace Aw.Judge.Infrastructure.Images;

/// <summary>
/// The image cases embedded in the binary from tests/fixtures/judge/images: '&lt;name&gt;.&lt;label&gt;.png' is the case 'name' that
/// visual-critique should call '&lt;label&gt;' (looks-right or looks-off). A file with another label is left out.
/// </summary>
internal sealed class EmbeddedImageCaseSet : IImageCaseSet
{
    private const string Prefix = "judge-images/";

    private const string Extension = ".png";

    private static readonly string[] _labels = ["looks-right", "looks-off"];

    private readonly Lazy<List<ImageCase>> _cases = new(Load);

    /// <inheritdoc />
    public IReadOnlyList<ImageCase> Cases => _cases.Value;

    private static List<ImageCase> Load()
    {
        var assembly = typeof(EmbeddedImageCaseSet).Assembly;
        List<ImageCase> cases = [];
        foreach (var resource in assembly.GetManifestResourceNames().Order(StringComparer.Ordinal))
        {
            if (!resource.StartsWith(Prefix, StringComparison.Ordinal) || !resource.EndsWith(Extension, StringComparison.Ordinal))
            {
                continue;
            }

            var stem = resource[Prefix.Length..^Extension.Length];
            var dot = stem.LastIndexOf('.');
            if (dot <= 0 || !_labels.Contains(stem[(dot + 1)..], StringComparer.Ordinal))
            {
                continue;
            }

            using var stream = assembly.GetManifestResourceStream(resource)!;   // why: the name came from this assembly's manifest
            using var bytes = new MemoryStream();
            stream.CopyTo(bytes);
            cases.Add(new ImageCase { Name = stem[..dot], Expected = stem[(dot + 1)..], Bytes = bytes.ToArray() });
        }

        return cases;
    }
}
