using System.Security.Cryptography;
using System.Text;
using Aw.Kernel;
using Aw.Sindri.Core;
using Aw.Sindri.Core.Toolkits;

namespace Aw.Sindri.Infrastructure.Toolkits;

/// <summary>A toolkit's folder on disk: the source hash is SHA-256 over every file's relative path and bytes, in path order, leaving out build output (artifacts, TestResults, .git at the top; bin and obj at any depth); the binary is the newest file named after the toolkit under artifacts/bin/&lt;project&gt; (the scaffold's UseArtifactsOutput), else under bin.</summary>
internal sealed class FileToolkitWorkspace : IToolkitWorkspace
{
    /// <summary>Folders left out at the top of the toolkit's folder: the scaffold's build output, test results and git.</summary>
    private static readonly string[] _skippedAtTop = ["artifacts", "TestResults", ".git"];

    /// <summary>Folders left out at any depth: a project's own build output when it builds without the artifacts layout.</summary>
    private static readonly string[] _skippedAnywhere = ["bin", "obj"];

    public async ValueTask<Result<string>> SourceHash(string dir, CancellationToken cancellationToken)
    {
        try
        {
            using var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
            foreach (var file in Directory.EnumerateFiles(dir, "*", SearchOption.AllDirectories).Select(f => Path.GetRelativePath(dir, f)).Where(static r => !IsSkipped(r)).Order(StringComparer.Ordinal))
            {
                hash.AppendData(Encoding.UTF8.GetBytes(file.Replace('\\', '/') + "\n"));
                hash.AppendData(await File.ReadAllBytesAsync(Path.Combine(dir, file), cancellationToken).ConfigureAwait(false));
            }

            return Convert.ToHexStringLower(hash.GetHashAndReset());
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            return SindriErrors.ToolkitNotBuilt.With($"the source in {dir} can't be read: {e.Message}");
        }
    }

    public Result<string> BinaryOf(string dir) => ResolveBinary(dir);

    public Result<string> TestsProjectOf(string dir)
    {
        try
        {
            var projects = Directory.EnumerateDirectories(dir)
                .Where(static d => !IsSkipped(Path.GetFileName(d)))
                .SelectMany(static d => Directory.EnumerateFiles(d, "*.Toolkit.Tests.csproj", SearchOption.TopDirectoryOnly))
                .Select(f => Path.GetRelativePath(dir, f).Replace('\\', '/'))
                .Order(StringComparer.Ordinal)
                .ToList();
            return projects.Count == 1
                ? projects[0]
                : SindriErrors.ToolkitTestsFailed.With($"{dir} needs exactly one *.Toolkit.Tests.csproj in a folder right under it, and has {projects.Count.ToString(System.Globalization.CultureInfo.InvariantCulture)}");
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            return SindriErrors.ToolkitTestsFailed.With($"the tests project of {dir} can't be found: {e.Message}");
        }
    }

    /// <summary>The toolkit's apphost: the file named by the folder's one *.Toolkit.csproj (its AssemblyName, else the csproj's own name) under bin, the newest when a build left several.</summary>
    /// <param name="dir">The toolkit's folder.</param>
    /// <returns>The absolute path, or SND-TK-003 saying which part is missing.</returns>
    internal static Result<string> ResolveBinary(string dir)
    {
        try
        {
            var projects = Directory.GetFiles(dir, "*.Toolkit.csproj", SearchOption.TopDirectoryOnly);
            if (projects.Length != 1)
            {
                return SindriErrors.ToolkitManifestInvalid.With($"{dir} needs exactly one *.Toolkit.csproj to name its binary, and has {projects.Length.ToString(System.Globalization.CultureInfo.InvariantCulture)}");
            }

            var assembly = AssemblyName(projects[0]);
            var file = OperatingSystem.IsWindows() ? assembly + ".exe" : assembly;
            // The scaffold builds into artifacts/bin/<project>/<config>/; the tests project's folder holds a copy of the apphost that is not the one to run.
            var artifacts = Path.Combine(dir, "artifacts", "bin", Path.GetFileNameWithoutExtension(projects[0]));
            var bin = Path.Combine(dir, "bin");
            var found = Newest(artifacts, file) ?? Newest(bin, file);
            return found ?? (Result<string>)SindriErrors.ToolkitManifestInvalid.With($"the build left no binary named {file} under {artifacts} or {bin}");
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException or System.Xml.XmlException)
        {
            return SindriErrors.ToolkitManifestInvalid.With($"the binary of {dir} can't be found: {e.Message}");
        }
    }

    /// <summary>The binary's path, or null (what a start needs; <see cref="ResolveBinary"/> says why not).</summary>
    /// <param name="dir">The toolkit's folder.</param>
    /// <returns>The absolute path, or null.</returns>
    internal static string? FindBinary(string dir)
    {
        var found = ResolveBinary(dir);
        return found.IsSuccess ? found.Value : null;
    }

    private static string? Newest(string folder, string file) => Directory.Exists(folder)
        ? Directory.EnumerateFiles(folder, file, SearchOption.AllDirectories).OrderByDescending(static f => File.GetLastWriteTimeUtc(f)).FirstOrDefault()
        : null;

    private static string AssemblyName(string csproj)
    {
        // A literal value is read as written; an MSBuild property such as $(MSBuildProjectName).Toolkit is taken literally too, which is fine because the scaffold writes a literal name.
        var declared = System.Xml.Linq.XDocument.Load(csproj).Descendants("AssemblyName").Select(static e => e.Value.Trim()).FirstOrDefault(static v => v.Length > 0);
        return declared ?? Path.GetFileNameWithoutExtension(csproj);
    }

    private static bool IsSkipped(string relative)
    {
        var parts = relative.Split('/', '\\');
        return _skippedAtTop.Contains(parts[0], StringComparer.Ordinal) || parts.Any(static p => _skippedAnywhere.Contains(p, StringComparer.Ordinal));
    }
}
