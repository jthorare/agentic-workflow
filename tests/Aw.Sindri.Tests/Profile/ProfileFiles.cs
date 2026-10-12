using System.Text.Json;
using Aw.Kernel.Json;
using Aw.Sindri.Host;
using Aw.Sindri.Infrastructure;
using Aw.Sindri.Infrastructure.Profile;

namespace Aw.Sindri.Tests.Profile;

/// <summary>A profile file and its approvals in a temp home, over the real adapters.</summary>
internal sealed class ProfileFiles : IDisposable
{
    private readonly Ledger.TempHome? _home;

    /// <summary>Over a fresh temp home, deleted on dispose.</summary>
    public ProfileFiles()
        : this(new Ledger.TempHome())
    {
    }

    /// <summary>Over <paramref name="home"/>, which the caller owns.</summary>
    public ProfileFiles(string home)
        : this(home, owned: null)
    {
    }

    private ProfileFiles(Ledger.TempHome home)
        : this(home.Path, home)
    {
    }

    private ProfileFiles(string home, Ledger.TempHome? owned)
    {
        _home = owned;
        Settings = new SindriSettings(home, Path.Combine(home, "profile", "profile.yaml"));
        Source = new YamlProfileSource(Settings, Options);
        Approvals = new FileApprovals(Settings, Options);
    }

    public static JsonSerializerOptions Options { get; } = AwJson.CreateOptions(SindriJsonContext.Default);

    public SindriSettings Settings { get; }

    public YamlProfileSource Source { get; }

    public FileApprovals Approvals { get; }

    public void Write(string yaml)
    {
        _ = Directory.CreateDirectory(Path.GetDirectoryName(Settings.ProfilePath)!);   // why: the profile path has a parent
        File.WriteAllText(Settings.ProfilePath, yaml);
    }

    public void Dispose() => _home?.Dispose();
}
