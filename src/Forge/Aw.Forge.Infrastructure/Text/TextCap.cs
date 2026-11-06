using System.Text;
using Aw.Forge.Core;

namespace Aw.Forge.Infrastructure.Text;

/// <summary>Cuts a file's text to <see cref="ForgeLimits.FileTextBytes"/> bytes of UTF-8, never inside a character.</summary>
internal static class TextCap
{
    internal static string Cut(string text)
    {
        if (Encoding.UTF8.GetByteCount(text) <= ForgeLimits.FileTextBytes)
        {
            return text;
        }

        var bytes = Encoding.UTF8.GetBytes(text);
        var length = ForgeLimits.FileTextBytes;
        while (length > 0 && (bytes[length] & 0xC0) == 0x80)
        {
            length--;
        }

        return Encoding.UTF8.GetString(bytes, 0, length);
    }
}
