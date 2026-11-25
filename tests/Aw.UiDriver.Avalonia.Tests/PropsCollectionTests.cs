using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Media;
using Aw.UiDriver.Core.Protocol;

namespace Aw.UiDriver.Avalonia.Tests;

/// <summary>`props` prints a collection-valued property as its items' text joined with ", ", not as the collection's type name (#4270).</summary>
public sealed class PropsCollectionTests
{
    [Fact, Trait("Lane", "integration")]
    public async Task Props_prints_font_features_and_a_list_property_as_their_items()
    {
        var screen = await TestScreen.OpenAsync();
        _ = await TestScreen.OnUi(() =>
        {
            var text = new SelectableTextBlock
            {
                Text = "code",
                FontFeatures = [FontFeature.Parse("-calt"), FontFeature.Parse("-liga")],
            };
            AutomationProperties.SetAutomationId(text, "code");
            var tagged = new Tagged { Tags = ["alpha", "beta"] };
            AutomationProperties.SetAutomationId(tagged, "tagged");
            if (screen.Window.Content is StackPanel form)
            {
                form.Children.Add(text);
                form.Children.Add(tagged);
            }

            return true;
        });
        var host = new DriverHost(() => screen.Window, TimeProvider.System);

        var code = (await host.Props(new PropsRequest(new Selector(AutomationId: "code")))).Value.Properties;
        var tags = (await host.Props(new PropsRequest(new Selector(AutomationId: "tagged")))).Value.Properties;

        Assert.Equal(("-calt, -liga", "code"), (code["FontFeatures"], code["Text"]));
        Assert.Equal("alpha, beta", tags["Tags"]);
    }

    [Fact, Trait("Lane", "integration")]
    public async Task Props_prints_the_first_items_of_a_long_list_and_how_many_more_there_are()
    {
        var screen = await TestScreen.OpenAsync();
        _ = await TestScreen.OnUi(() =>
        {
            var tagged = new Tagged { Tags = [.. Enumerable.Range(0, PropertyValues.MaxItems + 5).Select(static i => "t" + i.ToString(System.Globalization.CultureInfo.InvariantCulture))] };
            AutomationProperties.SetAutomationId(tagged, "long");
            if (screen.Window.Content is StackPanel form)
            {
                form.Children.Add(tagged);
            }

            return true;
        });
        var host = new DriverHost(() => screen.Window, TimeProvider.System);

        var tags = (await host.Props(new PropsRequest(new Selector(AutomationId: "long")))).Value.Properties["Tags"];

        Assert.Equal(("t0, t1, ", ", t19, +5 more"), (tags[..8], tags[^14..]));
    }

    /// <summary>A control with a list-valued styled property.</summary>
    private sealed class Tagged : Control
    {
        public static readonly StyledProperty<IReadOnlyList<string>?> TagsProperty =
            AvaloniaProperty.Register<Tagged, IReadOnlyList<string>?>(nameof(Tags));

        public IReadOnlyList<string>? Tags
        {
            get => GetValue(TagsProperty);
            set => SetValue(TagsProperty, value);
        }
    }
}
