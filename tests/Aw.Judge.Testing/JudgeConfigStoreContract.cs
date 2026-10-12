using Aw.Judge.Core.Config;
using Aw.Judge.Core.Ports;
using Xunit;

namespace Aw.Judge.Testing;

/// <summary>
/// The IJudgeConfigStore contract: the same tests run against the in-memory fake and every real adapter
/// (.agents/rules/csharp.md, Seams). A derived class supplies a fresh, empty store and one that can't be written.
/// </summary>
public abstract class JudgeConfigStoreContract
{
    private static readonly CancellationToken _none = CancellationToken.None;

    /// <summary>A fresh, empty store.</summary>
    /// <param name="cancellationToken">Cancels the setup.</param>
    /// <returns>The store.</returns>
    protected abstract ValueTask<IJudgeConfigStore> Create(CancellationToken cancellationToken);

    /// <summary>A store that can't be written (its location can't be created).</summary>
    /// <param name="cancellationToken">Cancels the setup.</param>
    /// <returns>The store.</returns>
    protected abstract ValueTask<IJudgeConfigStore> CreateUnavailable(CancellationToken cancellationToken);

    /// <summary>An empty store loads the defaults: wake-gate enabled at 0.7, no provider or prompt-sort block.</summary>
    /// <returns>A task.</returns>
    [Fact]
    public async Task An_empty_store_loads_the_defaults()
    {
        var store = await Create(_none);

        var loaded = await store.Load(_none);

        Expect.That(loaded.IsSuccess && ConfigText.Of(loaded.Value) == ConfigText.Of(JudgeConfig.Default), "the defaults");
    }

    /// <summary>A saved config loads back with every question and both blocks.</summary>
    /// <returns>A task.</returns>
    [Fact]
    public async Task A_saved_config_loads_back_with_its_blocks()
    {
        var store = await Create(_none);
        var config = new JudgeConfig(
            new Dictionary<string, QuestionConfig>(StringComparer.Ordinal)
            {
                ["wake-gate"] = new(Enabled: false, Threshold: 0.85),
                ["ask-check"] = new(Enabled: true, Threshold: 0.6) { Providers = ["jev", "rules"] },
            },
            new ProvidersConfig(["codex-cli", "claude-cli"], Jev: false),
            new PromptSortConfig(Enabled: true, BudgetMs: 800, CooldownPrompts: 3, new Dictionary<string, bool>(StringComparer.Ordinal) { ["brief"] = true }));

        Expect.That((await store.Save(config, _none)).IsSuccess, "the config is saved");

        var loaded = await store.Load(_none);
        Expect.That(loaded.IsSuccess && ConfigText.Of(loaded.Value) == ConfigText.Of(config), "it loads back with every field");
    }

    /// <summary>A store that can't be written returns an error, never throws.</summary>
    /// <returns>A task.</returns>
    [Fact]
    public async Task A_store_that_cant_be_written_returns_an_error()
    {
        var store = await CreateUnavailable(_none);

        var saved = await store.Save(JudgeConfig.Default, _none);

        Expect.That(saved.IsError, "an error, not an exception");
    }
}
