using System.Globalization;
using Aw.Evidence.Core.Browser;
using Microsoft.Playwright;
using Locator = Aw.Evidence.Core.Browser.Locator;

namespace Aw.Evidence.Infrastructure.Browser;

/// <summary>Maps Core's locators onto Playwright: test id, first visible text match, first CSS match, interactive element by index.</summary>
internal static class PlaywrightLocators
{
    /// <summary>The Playwright locator for <paramref name="target"/>.</summary>
    /// <param name="page">The page.</param>
    /// <param name="target">The element.</param>
    /// <returns>The locator.</returns>
    internal static ILocator Resolve(IPage page, Locator target) => target switch
    {
        TestIdLocator id => page.GetByTestId(id.TestId.Value),
        NthTestIdLocator nth => page.GetByTestId(nth.TestId.Value).Nth(nth.Index),
        RoleLocator role => Nth(page.GetByRole(RoleOf(role.Role), new() { Name = role.Name, Exact = true }), role.Nth),
        ExactTextLocator text => Nth(page.GetByText(text.Text, new() { Exact = true }), text.Nth),
        TextLocator text => page.GetByText(text.Text).Locator("visible=true").First,
        CssLocator css => page.Locator(css.Css).First,
        CandidateLocator candidate => page.Locator(PageScripts.Interactive).Nth(candidate.Index),
        _ => throw new ArgumentOutOfRangeException(nameof(target), target, "unknown locator"),
    };

    private static ILocator Nth(ILocator all, int? nth) => nth is { } n ? all.Nth(n - 1) : all;

    private static AriaRole RoleOf(string role) =>
        Enum.TryParse<AriaRole>(role, ignoreCase: true, out var parsed) ? parsed : throw new PlaywrightException($"unknown ARIA role '{role}'");

    /// <summary>For a candidate, checks the element still carries the test id it had when enumerated (the DOM didn't shift).</summary>
    /// <param name="page">The page.</param>
    /// <param name="target">The element.</param>
    /// <param name="timeout">How long to wait for it.</param>
    /// <returns>The locator to act on.</returns>
    /// <exception cref="PlaywrightException">The element moved or is missing.</exception>
    internal static async Task<ILocator> ResolveCheckedAsync(IPage page, Locator target, TimeSpan timeout)
    {
        var locator = Resolve(page, target);
        var (nth, all) = target switch
        {
            RoleLocator { Nth: { } n } role => (n, Resolve(page, role with { Nth = null })),
            ExactTextLocator { Nth: { } n } text => (n, Resolve(page, text with { Nth = null })),
            _ => (0, locator),
        };
        if (nth > 0)
        {
            // An explicit '#<n>' picks the nth match and fails clearly when fewer than n exist.
            try
            {
                await locator.WaitForAsync(new() { State = WaitForSelectorState.Attached, Timeout = Ms(timeout) }).ConfigureAwait(false);
            }
            catch (PlaywrightException ex)
            {
                var found = await all.CountAsync().ConfigureAwait(false);
                throw new PlaywrightException(string.Create(CultureInfo.InvariantCulture, $"asked for match #{nth} but only {found} found"), ex);
            }
        }

        if (target is CandidateLocator candidate)
        {
            var testId = await locator.GetAttributeAsync("data-testid", new() { Timeout = Ms(timeout) }).ConfigureAwait(false);
            if (!string.Equals(testId, candidate.ExpectedTestId?.Value, StringComparison.Ordinal))
            {
                throw new PlaywrightException(string.Create(CultureInfo.InvariantCulture, $"candidate {candidate.Index} is no longer the element it was (the DOM shifted)"));
            }
        }

        return locator;
    }

    /// <summary>The first visible element for a test id (a grid repeats test ids per cell, and a strict lookup throws on more than one match); any other locator as <see cref="Resolve"/> has it.</summary>
    /// <param name="page">The page.</param>
    /// <param name="target">The element.</param>
    /// <returns>The locator.</returns>
    internal static ILocator FirstVisible(IPage page, Locator target) => target switch
    {
        TestIdLocator id => page.GetByTestId(id.TestId.Value).Locator("visible=true").First,
        _ => Resolve(page, target),
    };

    /// <summary>The visible elements matching a test id (any other locator is its one first match).</summary>
    /// <param name="page">The page.</param>
    /// <param name="target">The element.</param>
    /// <returns>The locator, whose count is how many are visible.</returns>
    internal static ILocator Visible(IPage page, Locator target) => target switch
    {
        TestIdLocator id => page.GetByTestId(id.TestId.Value).Locator("visible=true"),
        _ => Resolve(page, target),
    };

    /// <summary>A timeout in Playwright's milliseconds.</summary>
    /// <param name="timeout">The timeout.</param>
    /// <returns>Milliseconds.</returns>
    internal static float Ms(TimeSpan timeout) => (float)timeout.TotalMilliseconds;
}
