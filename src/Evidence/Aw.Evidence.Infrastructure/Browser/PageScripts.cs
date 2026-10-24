namespace Aw.Evidence.Infrastructure.Browser;

/// <summary>The JavaScript the browser adapter evaluates in the page (the long ones are embedded Browser/Scripts/*.js files).</summary>
internal static class PageScripts
{
    /// <summary>The elements selector repair may pick from, in DOM order (CandidateLocator indexes into this list).</summary>
    internal const string Interactive = "button, a, input, [role], [data-testid]";

    /// <summary>Waits for web fonts.</summary>
    internal const string FontsReady = "() => document.fonts.ready.then(() => true)";

    /// <summary>True once the page is on the given path and has finished loading (the load event has fired).</summary>
    internal const string OnPathLoaded = "path => location.pathname === path && document.readyState === 'complete'";

    /// <summary>True once the page's path no longer contains the given text and the page has finished loading.</summary>
    internal const string OffPathLoaded = "text => !location.pathname.includes(text) && document.readyState === 'complete'";

    /// <summary>True once one query parameter of the page's URL has the given value and the page has finished loading; its argument is { param, value }.</summary>
    internal const string OnSearchParam =
        "({ param, value }) => new URL(location.href).searchParams.get(param) === value && document.readyState === 'complete'";

    /// <summary>Scrolls a container: end/start (horizontal), bottom/top (vertical) or a horizontal pixel offset; its argument is the position.</summary>
    internal const string ScrollTo =
        "(node, pos) => { if (pos === 'end') node.scrollLeft = node.scrollWidth; else if (pos === 'start') node.scrollLeft = 0; else if (pos === 'bottom') node.scrollTop = node.scrollHeight; else if (pos === 'top') node.scrollTop = 0; else node.scrollLeft = Number(pos); return true; }";

    /// <summary>Resolves after the next animation frame.</summary>
    internal const string NextFrame = "() => new Promise((resolve) => requestAnimationFrame(() => resolve(true)))";

    /// <summary>Blurs the focused element.</summary>
    internal const string Blur = "() => { document.activeElement?.blur?.(); return true; }";

    /// <summary>True when the point holds something interactive (clicking there would change the page).</summary>
    internal const string ControlAt =
        "({ x, y }) => !!document.elementFromPoint(x, y)?.closest('button, a, input, select, textarea, label, summary, [role], [onclick], [tabindex]')";

    /// <summary>Scrolls the window and every scrolled element back to the top.</summary>
    internal const string ResetScroll =
        "() => { window.scrollTo(0, 0); document.querySelectorAll('*').forEach((e) => { if (e.scrollTop > 0) e.scrollTop = 0; }); return true; }";

    /// <summary>Visible interactive elements (Browser/Scripts/visible-candidates.js); its argument is <see cref="Interactive"/>.</summary>
    internal static string VisibleCandidates { get; } = Embedded("visible-candidates.js");

    /// <summary>The lint snapshot (Browser/Scripts/lint-snapshot.js).</summary>
    internal static string LintSnapshot { get; } = Embedded("lint-snapshot.js");

    private static string Embedded(string name)
    {
        using var stream = typeof(PageScripts).Assembly.GetManifestResourceStream("Aw.Evidence.Infrastructure.Browser.Scripts." + name)
            ?? throw new InvalidOperationException($"the embedded page script {name} is missing from the build");
        using var reader = new StreamReader(stream);
        return reader.ReadToEnd();
    }
}
