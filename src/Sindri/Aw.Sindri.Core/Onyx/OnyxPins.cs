using System.Globalization;
using Aw.Kernel;

namespace Aw.Sindri.Core.Onyx;

/// <summary>
/// The pins of Sindri-managed Onyx: the release, the commit its tag points to, the deployment files and images with
/// their hashes, and what the box needs (context/onyx.lock; spec §6). scripts/onyx-lock.sh check is the same check in bash: the two are held to the same malformed fixtures listed in scripts/tests/onyx-lock.test.sh and OnyxPinsParseTests (both split fields on space and tab only, allow a file path of [A-Za-z0-9._/-] only and refuse a line holding CR, VT or FF), which says nothing about input outside them, and the C# parser reports each refusal as SND-ONYX-004.
/// </summary>
/// <param name="Version">The release tag, vX.Y.Z.</param>
/// <param name="Commit">The commit the tag points to, 40 lowercase hex digits.</param>
/// <param name="Files">The deployment files to fetch, with their sha256.</param>
/// <param name="Images">Every image for each platform, by digest.</param>
/// <param name="DiskBytes">Free disk the setup needs.</param>
/// <param name="DockerMemoryBytes">The floor for `docker info` MemTotal; the setting the user is told is this rounded up to a whole GiB.</param>
public sealed record OnyxPins(
    string Version, string Commit, IReadOnlyList<OnyxFilePin> Files, IReadOnlyList<OnyxImagePin> Images, long DiskBytes, long DockerMemoryBytes)
{
    private const string ComposeFile = "docker_compose/docker-compose.yml";
    private static readonly char[] _separators = [' ', '\t'];
    private static readonly char[] _controls = ['\r', '\v', '\f'];
    private static readonly string[] _platforms = ["linux/arm64", "linux/amd64"];

    /// <summary>Reads the text of a lock file.</summary>
    /// <param name="text">The file's text: `#` comments and blank lines are ignored, the other rows are `version`, `file`, `image` and `need`.</param>
    /// <returns>The pins, or SND-ONYX-004 naming the first row that is malformed.</returns>
    public static Result<OnyxPins> Parse(string text)
    {
        ArgumentNullException.ThrowIfNull(text);
        if (text.IndexOf('\0', StringComparison.Ordinal) is var nul and >= 0)
        {
            var at = text.AsSpan(0, nul).Count('\n') + 1;
            return SindriErrors.OnyxPinsMalformed.With($"the Onyx pins are malformed: line {at.ToString(CultureInfo.InvariantCulture)}: the file contains a NUL byte");
        }

        var reader = new PinsReader();
        var number = 0;
        foreach (var line in text.Split('\n'))
        {
            number++;
            if (line.AsSpan().IndexOfAny(_controls) >= 0)
            {
                return SindriErrors.OnyxPinsMalformed.With($"the Onyx pins are malformed: line {number.ToString(CultureInfo.InvariantCulture)}: the line contains a CR, VT or FF control character");
            }

            var fields = line.Split(_separators, StringSplitOptions.RemoveEmptyEntries);   // space and tab only, as awk splits: a CR or a no-break space stays in its field
            if (fields.Length == 0 || fields[0].StartsWith('#'))
            {
                continue;
            }

            if (reader.Read(fields) is { } problem)
            {
                return SindriErrors.OnyxPinsMalformed.With($"the Onyx pins are malformed: line {number.ToString(CultureInfo.InvariantCulture)}: {problem}");
            }
        }

        return reader.Build();
    }

    private static bool IsHex(string value, int length) => value.Length == length && value.All(static c => c is (>= '0' and <= '9') or (>= 'a' and <= 'f'));

    private sealed class PinsReader
    {
        private readonly List<OnyxFilePin> _files = [];
        private readonly List<OnyxImagePin> _images = [];
        private readonly Dictionary<string, long> _needs = new(StringComparer.Ordinal);
        private string? _version;
        private string? _commit;

        public string? Read(string[] fields) => fields[0] switch
        {
            "version" => ReadVersion(fields),
            "file" => ReadFile(fields),
            "image" => ReadImage(fields),
            "need" => ReadNeed(fields),
            var kind => $"unknown row kind \"{kind}\"",
        };

        public Result<OnyxPins> Build()
        {
            var problem = Missing();
            return problem is not null || _version is null || _commit is null
                ? SindriErrors.OnyxPinsMalformed.With($"the Onyx pins are malformed: {problem ?? "no version row"}")
                : new OnyxPins(_version, _commit, _files, _images, _needs["disk-bytes"], _needs["docker-memory-bytes"]);
        }

        private string? Missing()
        {
            if (_version is null)
            {
                return "no version row";
            }

            if (_images.Count == 0)
            {
                return "no image rows";
            }

            if (!_files.Exists(static f => f.Path == ComposeFile))
            {
                return $"no file row for {ComposeFile}";
            }

            foreach (var repository in _images.Select(static i => i.Repository).Distinct(StringComparer.Ordinal))
            {
                foreach (var platform in _platforms.Where(p => !_images.Exists(i => i.Repository == repository && i.Platform == p)))
                {
                    return $"{repository} has no {platform} row";
                }
            }

            return _needs.ContainsKey("disk-bytes") ? _needs.ContainsKey("docker-memory-bytes") ? null : "no need docker-memory-bytes row" : "no need disk-bytes row";
        }

        private string? ReadVersion(string[] fields)
        {
            if (_version is not null)
            {
                return "a second version row";
            }

            if (fields.Length != 3)
            {
                return $"version wants 2 fields (tag commit), got {(fields.Length - 1).ToString(CultureInfo.InvariantCulture)}";
            }

            if (!IsTag(fields[1]))
            {
                return $"version tag \"{fields[1]}\" is not vX.Y.Z";
            }

            if (!IsHex(fields[2], 40))
            {
                return "version commit is not 40 lowercase hex digits";
            }

            (_version, _commit) = (fields[1], fields[2]);
            return null;
        }

        private string? ReadFile(string[] fields)
        {
            if (fields.Length != 3 || !IsRelativePath(fields[1]) || !IsHex(fields[2], 64))
            {
                return "file wants a relative path of [A-Za-z0-9._/-] with non-empty segments (no ..) and a 64-digit lowercase hex sha256";
            }

            if (_files.Exists(f => f.Path == fields[1]))
            {
                return $"{fields[1]} is listed twice";
            }

            _files.Add(new OnyxFilePin(fields[1], fields[2]));
            return null;
        }

        private string? ReadImage(string[] fields)
        {
            if (!IsImageRow(fields))
            {
                return "image wants a repository (^[a-z0-9][a-z0-9._/-]*$), a tag, linux/arm64 or linux/amd64, and a sha256: digest";
            }

            if (_images.Exists(i => i.Repository == fields[1] && i.Platform == fields[3]))
            {
                return $"{fields[1]} {fields[3]} is listed twice";
            }

            if (_images.Find(i => i.Repository == fields[1]) is { } other && other.Tag != fields[2])
            {
                return $"{fields[1]} is listed with tags {other.Tag} and {fields[2]}";
            }

            _images.Add(new OnyxImagePin(fields[1], fields[2], fields[3], fields[4]));
            return null;
        }

        private string? ReadNeed(string[] fields)
        {
            if (fields.Length != 3 || fields[1] is not ("disk-bytes" or "docker-memory-bytes")
                || fields[2].Length > 18 || !long.TryParse(fields[2], NumberStyles.None, CultureInfo.InvariantCulture, out var bytes) || bytes == 0)
            {
                return "need wants disk-bytes or docker-memory-bytes and a positive integer of at most 18 digits";
            }

            return _needs.TryAdd(fields[1], bytes) ? null : $"{fields[1]} is listed twice";
        }

        private static bool IsImageRow(string[] fields)
            => fields.Length == 5 && IsRepository(fields[1]) && IsImageTag(fields[2]) && _platforms.Contains(fields[3], StringComparer.Ordinal)
                && fields[4].StartsWith("sha256:", StringComparison.Ordinal) && IsHex(fields[4][7..], 64);

        private static bool IsRelativePath(string path)
            => path.All(static c => char.IsAsciiLetterOrDigit(c) || c is '.' or '_' or '/' or '-')
                && !path.StartsWith('/') && path.Split('/').All(static s => s.Length > 0 && s != "..");

        private static bool IsRepository(string name)
            => name.Length > 0 && (char.IsAsciiLetterLower(name[0]) || char.IsAsciiDigit(name[0]))
                && name.All(static c => char.IsAsciiLetterLower(c) || char.IsAsciiDigit(c) || c is '.' or '_' or '/' or '-');

        private static bool IsImageTag(string tag)
            => tag.Length is > 0 and <= 128 && (char.IsAsciiLetterOrDigit(tag[0]) || tag[0] == '_')
                && tag.All(static c => char.IsAsciiLetterOrDigit(c) || c is '_' or '.' or '-');

        private static bool IsTag(string tag)
        {
            var parts = tag.Length > 1 && tag[0] == 'v' ? tag[1..].Split('.') : [];
            return parts.Length == 3 && parts.All(static p => p.Length > 0 && p.All(char.IsAsciiDigit));
        }
    }
}
