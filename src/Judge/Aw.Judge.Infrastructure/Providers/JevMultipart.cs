using System.Net.Http.Headers;
using Aw.Judge.Core.Images;

namespace Aw.Judge.Infrastructure.Providers;

/// <summary>Jev's request bodies: the JSON one, and the multipart one that carries images.</summary>
internal static class JevMultipart
{
    /// <summary>A JSON body.</summary>
    /// <param name="json">The body's bytes.</param>
    /// <returns>The content; the request that holds it disposes it.</returns>
    public static ByteArrayContent Json(byte[] json)
    {
        var content = new ByteArrayContent(json);
        content.Headers.ContentType = new MediaTypeHeaderValue("application/json");
        return content;
    }

    /// <summary>Part 'request' holds the body; each image is a file part named, and filed, by its reference (the file name is never a path).</summary>
    /// <param name="json">The body's bytes.</param>
    /// <param name="images">The images, in order.</param>
    /// <param name="references">Their references, in the same order.</param>
    /// <returns>The content; the request that holds it disposes it and its parts.</returns>
    public static MultipartFormDataContent Build(byte[] json, IReadOnlyList<ImageAttachment> images, IReadOnlyList<string> references)
    {
#pragma warning disable CA2000 // each part is owned by the multipart content, which the request disposes
        var content = new MultipartFormDataContent { { Json(json), "request" } };
        for (var i = 0; i < images.Count; i++)
        {
            var part = new ByteArrayContent(images[i].Bytes.ToArray());
            part.Headers.ContentType = new MediaTypeHeaderValue(images[i].Format == ImageFormat.Png ? "image/png" : "image/jpeg");
            content.Add(part, references[i], references[i]);
        }
#pragma warning restore CA2000
        return content;
    }
}
