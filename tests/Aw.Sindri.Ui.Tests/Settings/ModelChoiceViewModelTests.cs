using Aw.Kernel.Testing;
using Aw.Sindri.Ui.Api;
using Aw.Sindri.Ui.Testing;
using Aw.Sindri.Ui.ViewModels.Settings;
using Microsoft.Extensions.Time.Testing;

namespace Aw.Sindri.Ui.Tests.Settings;

/// <summary>Settings' model-choice line (model-pick spec 8): its three states in words from what Sindri serves, the sentence, and a Copy of the lines.</summary>
[Lane(TestLane.Unit)]
public sealed class ModelChoiceViewModelTests
{
    private readonly FakeClipboard _clipboard = new();
    private readonly FakeTimeProvider _clock = new(new DateTimeOffset(2026, 10, 27, 12, 0, 0, TimeSpan.Zero));

    private ModelChoiceViewModel Line(string modelPick, string? source = "/home/thor/profile.yaml")
    {
        var line = new ModelChoiceViewModel(_clipboard, _clock);
        line.Show(new ProfileView("b", Problem: null, source, modelPick));
        return line;
    }

    [Fact]
    public void On_says_model_choice_is_on() => Assert.Equal("Model choice: on", Line("on").Line);

    [Fact]
    public void Off_says_steps_use_the_agents_default_with_no_jev_call() =>
        Assert.Equal("Model choice: off. Steps use the agent's default model; nothing picks one per step.", Line("off").Line);

    [Fact]
    public void The_sentence_says_what_jev_reads_where_to_change_it_and_that_retention_is_the_providers() =>
        Assert.Equal("Jev reads each step's brief and files to pick the cheapest model that works. Change it in the profile; it takes effect when you save. Retention is the provider's.", Line("on").Sentence);

    [Fact]
    public async Task Copy_puts_the_profile_path_and_the_command_on_the_clipboard()
    {
        var line = Line("off");

        await line.Copy(TestContext.Current.CancellationToken);

        Assert.Equal("/home/thor/profile.yaml\nsindri profile validate", _clipboard.Text);
        Assert.Equal("Copied", line.CopyLabel);
        _clock.Advance(TimeSpan.FromSeconds(2));
        await Task.Yield();
        Assert.Equal("Copy", line.CopyLabel);
    }

    [Fact]
    public async Task Copy_without_a_known_profile_file_puts_the_command()
    {
        var line = Line("off", source: null);

        await line.Copy(TestContext.Current.CancellationToken);

        Assert.Equal("sindri profile validate", _clipboard.Text);
    }
}
