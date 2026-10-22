using System.Text;
using Aw.Sindri.Core.Sessions;

namespace Aw.Sindri.Infrastructure.Sessions;

/// <summary>Reads a KERN_PROCARGS2 block: argc (int32), the executable's path, NUL padding, then argc arguments and the environment, each NUL-ended.</summary>
internal static class MacArgumentBlock
{
    /// <summary>Takes argv[0] and the allowlisted environment names out of a block. The other arguments are skipped by their count, never read (one may hold a prompt, or a copy of an environment name), and every other environment name is dropped as it is met.</summary>
    /// <param name="block">The block.</param>
    /// <returns>argv[0] and the allowlisted environment, or null when the block isn't one.</returns>
    internal static MacArguments? Parse(ReadOnlySpan<byte> block)
    {
        if (block.Length < sizeof(int) || BitConverter.ToInt32(block[..sizeof(int)]) is not (var argc and >= 0))
        {
            return null;
        }

        var rest = block[sizeof(int)..];
        var pathEnd = rest.IndexOf((byte)0);
        if (pathEnd < 0)
        {
            return null;
        }

        rest = rest[pathEnd..].TrimStart((byte)0);
        var argv0 = Arguments(ref rest, argc);
        return new MacArguments(argv0, Environment(rest));
    }

    private static string? Arguments(ref ReadOnlySpan<byte> rest, int argc)
    {
        string? argv0 = null;
        for (var index = 0; index < argc && !rest.IsEmpty; index++)
        {
            var end = rest.IndexOf((byte)0);
            var argument = end < 0 ? rest : rest[..end];
            if (index == 0 && argument.Length > 0)
            {
                argv0 = Encoding.UTF8.GetString(argument);
            }

            rest = end < 0 ? [] : rest[(end + 1)..];
        }

        return argv0;
    }

    private static Dictionary<string, string> Environment(ReadOnlySpan<byte> rest)
    {
        var kept = new Dictionary<string, string>(StringComparer.Ordinal);
        while (!rest.IsEmpty)
        {
            var end = rest.IndexOf((byte)0);
            var entry = end < 0 ? rest : rest[..end];
            var equals = entry.IndexOf((byte)'=');
            if (equals > 0 && Encoding.UTF8.GetString(entry[..equals]) is var name && EnvironmentAllowlist.Names.Contains(name))
            {
                kept[name] = Encoding.UTF8.GetString(entry[(equals + 1)..]);
            }

            rest = end < 0 ? [] : rest[(end + 1)..];
        }

        return kept;
    }
}
