using Aw.Judge.Core.Config;
using Aw.Judge.Features.GetConfig;
using Aw.Judge.Testing;

namespace Aw.Judge.Tests.GetConfig;

public sealed class GetConfigTests
{
    [Fact, Trait("Lane", "unit")]
    public async Task GetConfig_returns_the_effective_config()
    {
        var stored = JudgeConfig.Default with { Providers = new ProvidersConfig(["codex-cli"], Jev: null) };
        var store = new InMemoryJudgeConfigStore { Stored = stored };

        var result = await new GetConfigService(store).Handle(A.GetConfigRequest().Build(), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Same(stored, result.Value.Config);
        Assert.Null(result.Value.Warning);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task GetConfig_returns_the_defaults_and_a_warning_when_the_stored_config_is_invalid()
    {
        var store = new InMemoryJudgeConfigStore { InvalidReason = "invalid config config.json: threshold must be between 0 and 1, not 2" };

        var result = await new GetConfigService(store).Handle(A.GetConfigRequest().Build(), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(ConfigText.Of(JudgeConfig.Default), ConfigText.Of(result.Value.Config));
        Assert.Equal("invalid config config.json: threshold must be between 0 and 1, not 2", result.Value.Warning);
    }
}
