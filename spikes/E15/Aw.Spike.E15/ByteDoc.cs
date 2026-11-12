using System.Text;

namespace Aw.Spike.E15;

/// <summary>Bytes in, text out, bytes back: keeps the BOM that AvaloniaEdit never sees. Strict UTF-8; invalid bytes return null (read-only note, SND-NOTE-026).</summary>
internal sealed record ByteDoc(bool HasBom, string Text)
{
    private static readonly byte[] Bom = [0xEF, 0xBB, 0xBF];
    private static readonly UTF8Encoding Strict = new(false, true);

    public static ByteDoc? Read(byte[] bytes)
    {
        var bom = bytes.Length >= 3 && bytes[0] == Bom[0] && bytes[1] == Bom[1] && bytes[2] == Bom[2];
        try
        {
            return new ByteDoc(bom, Strict.GetString(bytes, bom ? 3 : 0, bytes.Length - (bom ? 3 : 0)));
        }
        catch (DecoderFallbackException)
        {
            return null;
        }
    }

    public byte[] ToBytes(string text)
    {
        var body = new UTF8Encoding(false).GetBytes(text);
        return HasBom ? [.. Bom, .. body] : body;
    }
}
