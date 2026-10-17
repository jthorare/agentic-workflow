using System.Security.Cryptography;
using System.Text;

namespace Aw.Sindri.Core.Onyx;

/// <summary>The steps of `sindri onyx up` that state.json records, and the stamp of a step's inputs.</summary>
public static class OnyxSteps
{
    /// <summary>Spec §4.1 step 2: the pinned deployment files are fetched and verified.</summary>
    public const string Fetch = "fetch";

    /// <summary>Step 3: .env and the compose override are rendered.</summary>
    public const string Config = "config";

    /// <summary>Step 4: the images are pulled by digest and checked.</summary>
    public const string Pull = "pull";

    /// <summary>Step 5: the stack is started and answers with the pinned version.</summary>
    public const string Start = "start";

    /// <summary>Gets the steps in the order they run; each one's stamp is built from the one before.</summary>
    public static IReadOnlyList<string> Order { get; } = [Fetch, Config, Pull, Start];

    /// <summary>Hashes a step's inputs, so a rerun can tell whether the step ran with the same ones.</summary>
    /// <param name="parts">The inputs, in a fixed order.</param>
    /// <returns>The sha256 of the parts joined with a newline, as 64 lowercase hex digits.</returns>
    public static string Stamp(params string[] parts)
    {
        ArgumentNullException.ThrowIfNull(parts);
        return Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(string.Join('\n', parts))));
    }
}
