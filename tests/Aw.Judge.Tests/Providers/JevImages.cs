using System.Globalization;
using System.Security.Cryptography;
using Aw.Judge.Core.Images;
using Aw.Judge.Core.Secrets;
using Aw.Judge.Infrastructure.Providers;
using Aw.Judge.Testing;
using Aw.Judge.Tests.Images;

namespace Aw.Judge.Tests.Providers;

/// <summary>What the Jev image tests share: a provider over a handler, Jev's answers and image attachments built in memory.</summary>
internal static class JevImages
{
    public const string Key = "jev-test-key";

    public static JevProvider Jev(HttpMessageHandler handler) =>
        new(new HttpClient(handler), new FakeSecretSource(new Dictionary<SecretName, string> { [SecretName.TypeSafeApiKey] = Key }), new FakeInputRedactor());

    /// <summary>Jev's answer to the 'decision' choice question: <paramref name="choice"/> at confidence 0.9.</summary>
    public static string Choice(string choice) => Choice(choice, "0.9");

    /// <summary>Jev's answer to the 'decision' choice question: <paramref name="choice"/> at <paramref name="confidence"/> (as JSON).</summary>
    public static string Choice(string choice, string confidence) =>
        $$$"""{"answers":{"decision":{"type":"choice","choice":"{{{choice}}}","confidence":{{{confidence}}}}},"usage":{"input_tokens":12,"output_tokens":1}}""";

    /// <summary>A PNG attachment of the given size, as the image reader would have made it.</summary>
    public static ImageAttachment Attachment(string path, int width, int height, string tag = "") => Attachment(path, ImageBytes.Png(width, height, tag), width, height);

    public static ImageAttachment Attachment(string path, byte[] bytes, int width, int height) => new()
    {
        Path = path,
        Sha256 = Sha256(bytes),
        Bytes = bytes,
        Width = width,
        Height = height,
        Format = ImageFormat.Png,
    };

    public static string Sha256(byte[] bytes) => Convert.ToHexStringLower(SHA256.HashData(bytes));

    /// <summary>The reference Jev's request names an image by: aw-image-&lt;n&gt;-&lt;first 12 hex of its sha256&gt;.</summary>
    public static string Reference(int number, byte[] bytes) => string.Create(CultureInfo.InvariantCulture, $"aw-image-{number}-{Sha256(bytes)[..12]}");
}
