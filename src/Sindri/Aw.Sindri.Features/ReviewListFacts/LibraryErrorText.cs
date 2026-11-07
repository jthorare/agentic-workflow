using System.Globalization;
using Aw.Context.Core.Library;

namespace Aw.Sindri.Features.ReviewListFacts;

/// <summary>How the library routes write one design-doc finding (PR review page spec §6.1): the message, then the fix, with the record key in the message replaced by the file.</summary>
internal static class LibraryErrorText
{
    /// <summary>One finding as a sentence. Context writes a finding on a line as <c language="text">&lt;key&gt;:&lt;n&gt;: what</c>, which reads <c language="text">&lt;path&gt; line &lt;n&gt;: what</c> here; any other mention of the key becomes the path.</summary>
    /// <param name="error">The finding.</param>
    /// <param name="key">The doc's record key, which the message starts with.</param>
    /// <param name="path">The file as shown (home written <c language="text">~</c>).</param>
    /// <returns>The message and the fix joined by <c language="text">. </c> (a trailing <c language="text">.</c> of either dropped); the message alone, as it is, when the fix is empty.</returns>
    internal static string Of(DesignDocError error, string key, string path)
    {
        ArgumentNullException.ThrowIfNull(error);
        var message = error.Message;
        if (error.Line is { } line)
        {
            var at = key + ":" + line.ToString(CultureInfo.InvariantCulture) + ":";
            if (message.StartsWith(at, StringComparison.Ordinal))
            {
                message = $"{path} line {line.ToString(CultureInfo.InvariantCulture)}:" + message[at.Length..];
            }
        }

        message = message.Replace(key, path, StringComparison.Ordinal);
        return error.Fix.Length == 0 ? message : message.TrimEnd('.') + ". " + error.Fix.TrimEnd('.');
    }
}
