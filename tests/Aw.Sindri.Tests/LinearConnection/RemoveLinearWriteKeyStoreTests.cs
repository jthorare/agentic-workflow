using System.Text.Json;
using Aw.Context.Core;
using Aw.Context.Core.Facade;
using Aw.Context.Core.Models;
using Aw.Context.Features;
using Aw.Context.Infrastructure;
using Aw.Context.Testing.Engine;
using Aw.Context.Testing.Map;
using Aw.Context.Testing.Vectors;
using Aw.Kernel.Ids;
using Aw.Kernel.Json;
using Aw.Kernel.Testing;
using Aw.Sindri.Core;
using Aw.Sindri.Core.Profile;
using Aw.Sindri.Features.LinearConnection;
using Aw.Sindri.Infrastructure;
using Aw.Sindri.Infrastructure.Profile;
using Aw.Sindri.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Time.Testing;

namespace Aw.Sindri.Tests.LinearConnection;

/// <summary>
/// Removing the Linear write key through the real <see cref="CoreLinearConnectionStore"/>: a real Context engine reads the settings, the real YAML writer edits a profile file in a temp home, and the keychain is the fake.
/// The entry goes first; a keychain that refuses leaves the pointer in the file.
/// </summary>
[Trait("Lane", "integration")]
public sealed class RemoveLinearWriteKeyStoreTests : IDisposable
{
    private const string ReadPointer = "keychain:sindri-index/linear";
    private const string WritePointer = "keychain:sindri-index/linear-write";
    private const string ContextJson = $$$$"""{"sources":{"linear":{"teams":["ENG"],"token":"{{{{ReadPointer}}}}","writeToken":"{{{{WritePointer}}}}"}}}""";

    private readonly string _home = Directory.CreateTempSubdirectory("linear-remove-").FullName;
    private readonly ServiceProvider _provider;
    private readonly FakeSecretWriter _keychain = new();

    public RemoveLinearWriteKeyStoreTests()
    {
        _keychain.Stored[ReadPointer] = "lin_api_read";
        _keychain.Stored[WritePointer] = "lin_api_write";
        _provider = new ServiceCollection()
            .AddSingleton<TimeProvider>(new FakeTimeProvider(new DateTimeOffset(2026, 1, 1, 10, 0, 0, TimeSpan.Zero)))
            .AddSingleton(typeof(Microsoft.Extensions.Logging.ILogger<>), typeof(Microsoft.Extensions.Logging.Abstractions.NullLogger<>))
            .AddSingleton(FixtureMap.Json)
            .AddSingleton<IIdGenerator>(new SequentialIdGenerator())
            .AddSingleton<IModelPort>(new FakeModelPort())
            .AddSingleton<Context.Core.Vectors.IChunkEmbedder>(new FakeChunkEmbedder())
            .AddSingleton<Context.Core.Vectors.IQueryEmbedder>(new FakeQueryEmbedder())
            .AddSingleton<Context.Core.Vectors.IVectorScan>(new FakeVectorScan())
            .AddContextCore()
            .AddContextFeatures()
            .AddContextInfrastructure(name => name == "AW_SINDRI_HOME" ? _home : null)
            .BuildServiceProvider(new ServiceProviderOptions { ValidateOnBuild = false, ValidateScopes = true });
        Directory.CreateDirectory(Path.GetDirectoryName(ProfilePath)!);
        File.WriteAllText(ProfilePath, $"schemaVersion: 1\nuser: me\ncontext:\n  sources:\n    linear:\n      teams: [ENG]\n      token: \"{ReadPointer}\"\n      writeToken: \"{WritePointer}\"\n");
    }

    private string ProfilePath => Path.Combine(_home, "profile", "profile.yaml");

    public void Dispose()
    {
        _provider.Dispose();
        Directory.Delete(_home, recursive: true);
    }

    private CoreLinearConnectionStore Store()
    {
        var profile = new SindriProfile(1, "me", Context: JsonDocument.Parse(ContextJson).RootElement.Clone());
        var profiles = new InMemoryProfiles { Live = new LoadedProfile(profile, ProfileHashing.Of(AwJson.Serialize(profile, SliceHarness.Json)), ProfilePath) };
        var yaml = new YamlSourceTokenProfileWriter(new SindriSettings(_home, ProfilePath));
        return new CoreLinearConnectionStore(profiles, _provider.GetRequiredService<IContextEngine>(), _keychain, _keychain, yaml, yaml);
    }

    [Fact]
    public async Task Removing_the_write_key_deletes_the_keychain_entry_and_the_profiles_writeToken()
    {
        var removed = await Store().RemoveWrite(TestContext.Current.CancellationToken);

        Assert.True(removed.IsSuccess, removed.IsError ? removed.Error.Message : "");
        Assert.DoesNotContain(WritePointer, _keychain.Stored.Keys, StringComparer.Ordinal);
        Assert.Contains(ReadPointer, _keychain.Stored.Keys, StringComparer.Ordinal);
        var yaml = await File.ReadAllTextAsync(ProfilePath, TestContext.Current.CancellationToken);
        Assert.DoesNotContain("writeToken", yaml, StringComparison.Ordinal);
        Assert.Contains($"token: \"{ReadPointer}\"", yaml, StringComparison.Ordinal);
    }

    [Fact]
    public async Task A_keychain_that_refuses_is_SND_LINEAR_014_and_the_pointer_stays()
    {
        _keychain.Fails = true;

        var removed = await Store().RemoveWrite(TestContext.Current.CancellationToken);

        Assert.True(removed.IsError);
        Assert.Equal(SindriErrors.LinearNoKeychain.Code, removed.Error.Code);
        Assert.Contains($"writeToken: \"{WritePointer}\"", await File.ReadAllTextAsync(ProfilePath, TestContext.Current.CancellationToken), StringComparison.Ordinal);
        Assert.Contains(WritePointer, _keychain.Stored.Keys, StringComparer.Ordinal);
    }
}
