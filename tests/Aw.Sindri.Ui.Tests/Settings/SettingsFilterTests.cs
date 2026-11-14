using Aw.Kernel.Testing;
using Aw.Sindri.Ui.Testing;
using Aw.Sindri.Ui.ViewModels;
using Aw.Sindri.Ui.ViewModels.Settings;

namespace Aw.Sindri.Ui.Tests.Settings;

/// <summary>#3362: a filter box on Settings keeps the sections whose words contain what was typed, so Text size is a few letters away, not 14 Tabs.</summary>
[Lane(TestLane.Unit)]
public sealed class SettingsFilterTests
{
    private readonly InMemoryPreferencesStore _store = new();
    private readonly FakeMotionPreference _os = new();

    private SettingsViewModel Settings() => new(new Preferences(_store), _os, static _ => { });

    [Theory]
    [InlineData("", "ThisApp", true)]
    [InlineData("", "Sandbox", true)]
    [InlineData("text size", "ThisApp", true)]
    [InlineData("TEXT", "ThisApp", true)]
    [InlineData("  motion ", "ThisApp", true)]
    [InlineData("sandbox", "Sandbox", true)]
    [InlineData("linear", "Connections", true)]
    [InlineData("obsidian", "Connections", true)]
    [InlineData("model", "Choices", true)]
    [InlineData("checklist", "Setup", true)]
    [InlineData("library", "Library", true)]
    [InlineData("text size", "Sandbox", false)]
    [InlineData("text size", "Connections", false)]
    [InlineData("linear", "ThisApp", false)]
    [InlineData("sandbox", "Setup", false)]
    public void A_section_stays_when_the_filter_is_part_of_its_words(string filter, string section, bool stays)
        => Assert.Equal(stays, SettingsFilter.Matches(Enum.Parse<SettingsSection>(section), filter));

    [Fact]
    public void An_empty_filter_shows_This_app_and_reports_no_empty_result()
    {
        using var settings = Settings();

        Assert.True(settings.ShowsThisApp);
        Assert.False(settings.HasNoMatch);
    }

    [Fact]
    public void Typing_text_size_keeps_This_app()
    {
        using var settings = Settings();

        settings.Filter = "text size";

        Assert.True(settings.ShowsThisApp);
        Assert.False(settings.HasNoMatch);
    }

    [Fact]
    public void Typing_another_sections_word_hides_This_app()
    {
        using var settings = Settings();

        settings.Filter = "sandbox";

        Assert.False(settings.ShowsThisApp);
    }

    [Fact]
    public void A_filter_that_matches_no_section_says_so()
    {
        using var settings = Settings();

        settings.Filter = "zzzz";

        Assert.False(settings.ShowsThisApp);
        Assert.True(settings.HasNoMatch);
    }

    [Fact]
    public void Changing_the_filter_raises_changed_for_the_page()
    {
        using var settings = Settings();
        var raised = new List<string?>();
        settings.PropertyChanged += (_, e) => raised.Add(e.PropertyName);

        settings.Filter = "motion";

        Assert.Contains(nameof(SettingsViewModel.ShowsThisApp), raised, StringComparer.Ordinal);
        Assert.Contains(nameof(SettingsViewModel.HasNoMatch), raised, StringComparer.Ordinal);
    }

    [Theory]
    [InlineData("browse", "Browse Add folder")]
    [InlineData("reset", "Linear Reset key")]
    [InlineData("read key", "Linear read key Copy")]
    public void A_word_the_section_shows_matches_it_even_when_no_synonym_has_it(string typed, string shown)
    {
        Assert.True(SettingsFilter.Matches(SettingsSection.Library, typed, shown));
        Assert.False(SettingsFilter.Matches(SettingsSection.Library, typed));
    }

    [Fact]
    public void Clearing_the_filter_shows_every_section_the_page_has_again()
    {
        using var settings = Settings();
        settings.Filter = "zzzz";

        settings.Filter = string.Empty;

        Assert.True(settings.ShowsThisApp);
        Assert.False(settings.HasNoMatch);
    }
}
