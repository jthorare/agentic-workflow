using Aw.Evidence.Core.Design;
using Aw.Evidence.Core.Ids;
using Aw.Evidence.Core.Scripts;

namespace Aw.Evidence.Core.Browser;

/// <summary>How the browser finds one element.</summary>
public abstract record Locator
{
    /// <summary>The element with this data-testid.</summary>
    /// <param name="testId">The test id.</param>
    /// <returns>The locator.</returns>
    public static Locator ByTestId(TestId testId) => new TestIdLocator(testId);

    /// <summary>The first visible element showing this text (layouts often render a hidden copy too).</summary>
    /// <param name="text">The text.</param>
    /// <returns>The locator.</returns>
    public static Locator ByText(string text) => new TextLocator(text);

    /// <summary>The first element matching a CSS selector.</summary>
    /// <param name="css">The selector.</param>
    /// <returns>The locator.</returns>
    public static Locator ByCss(string css) => new CssLocator(css);

    /// <summary>The locator for a click, fill or press step's target.</summary>
    /// <param name="target">The parsed target.</param>
    /// <param name="index">The step's 0-based index among elements sharing a test id (ignored for role and text targets, which carry their own '#&lt;n&gt;').</param>
    /// <returns>The locator, or null for a target kind it doesn't know.</returns>
    public static Locator? For(StepTarget target, int? index) => target switch
    {
        TestIdTarget { TestId: var id } => index is { } nth ? new NthTestIdLocator(id, nth) : ByTestId(id),
        RoleTarget role => new RoleLocator(role.Role, role.Name, role.Nth),
        TextTarget text => new ExactTextLocator(text.Text, text.Nth),
        _ => null,
    };

    /// <summary>The locator for a manifest selector.</summary>
    /// <param name="selector">A well-formed selector.</param>
    /// <returns>The locator.</returns>
    public static Locator For(ElementSelector selector) =>
        selector.TestId is { } id ? ByTestId(id) : ByText(selector.Text ?? string.Empty);

    /// <summary>The locator for a form value's field.</summary>
    /// <param name="value">A well-formed form value.</param>
    /// <returns>The locator.</returns>
    public static Locator For(FormValue value) =>
        value.TestId is { } id ? ByTestId(id) : ByCss(value.Css ?? string.Empty);
}
