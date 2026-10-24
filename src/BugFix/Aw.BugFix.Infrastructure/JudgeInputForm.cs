using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Aw.BugFix.Core.Model;
using Aw.Kernel.Json;

namespace Aw.BugFix.Infrastructure;

/// <summary>The judge's canonical form of a resolution-check input, and the digest the judge stores for it (cutover spec §4.3 c).</summary>
internal static class JudgeInputForm
{
    private const int DigestLength = 16;

    /// <summary>Compact JSON through the AwJson profile, with the judge's check kind (ui goes out as ui-evidence); the judge digests its re-serialization of the input, so writing this form makes the digest the one it stores.</summary>
    /// <param name="input">The input built from state.</param>
    /// <param name="json">The package's AwJson options.</param>
    /// <returns>The UTF-8 bytes.</returns>
    internal static byte[] Canonical(ResolutionCheckInput input, JsonSerializerOptions json)
    {
        var node = JsonNode.Parse(AwJson.Serialize(input, json))!.AsObject(); // why: this process just wrote the object.
        if (input.CheckKind == CheckKind.Ui)
        {
            node["checkKind"] = "ui-evidence";
        }

        return Encoding.UTF8.GetBytes(node.ToJsonString(json));
    }

    /// <summary>The first 16 lowercase hex characters of the SHA-256 of the canonical bytes.</summary>
    /// <param name="canonical">The canonical bytes.</param>
    /// <returns>The digest.</returns>
    internal static string Digest(byte[] canonical) => Convert.ToHexStringLower(SHA256.HashData(canonical))[..DigestLength];
}
