using System.Reflection;
using System.Globalization;
using Avalonia;
using Avalonia.Controls;
using Aw.Kernel.Testing;
using Aw.Sindri.Ui.Platform;
using Aw.Sindri.Ui.Tests.Theme;

namespace Aw.Sindri.Ui.Tests.Architecture;

/// <summary>
/// A test file that constructs an Avalonia object (a control, a window, a brush, anything that derives from AvaloniaObject) must
/// run it inside the headless session's Dispatch. Setting a property on such an object from a plain xunit thread creates the UI
/// thread's dispatcher there, and a later Compositor in the session fails with "a different thread owns it" (#1640, #2948).
/// </summary>
[Lane(TestLane.Unit)]
public sealed class AvaloniaObjectsOnSessionGuardTests
{
    // A file that names one of these runs its Avalonia objects on the session's thread.
    private static readonly string[] _sessionMarkers = ["HeadlessUnitTestSession", "FrameHost"];

    [Fact]
    public void No_test_file_builds_an_Avalonia_object_without_using_the_headless_session()
    {
        var avaloniaTypes = AvaloniaObjectNames();
        var root = Path.Combine(UiSource.Root, "tests", "Aw.Sindri.Ui.Tests");
        var offenders = new List<string>();
        foreach (var file in Directory.EnumerateFiles(root, "*.cs", SearchOption.AllDirectories))
        {
            var relative = Path.GetRelativePath(root, file).Replace('\\', '/');
            if (relative.StartsWith("obj/", StringComparison.Ordinal) || relative.StartsWith("bin/", StringComparison.Ordinal))
            {
                continue;
            }

            var text = File.ReadAllText(file);
            if (_sessionMarkers.Any(m => text.Contains(m, StringComparison.Ordinal)))
            {
                continue;
            }

            var lines = File.ReadAllLines(file);
            for (var i = 0; i < lines.Length; i++)
            {
                foreach (var name in NewTypeNames(lines[i]).Where(avaloniaTypes.Contains))
                {
                    offenders.Add(relative + ":" + (i + 1).ToString(CultureInfo.InvariantCulture) + " new " + name);
                }
            }
        }

        Assert.True(offenders.Count == 0, "Avalonia objects built off the headless session's thread (wrap the body in HeadlessUnitTestSession.GetOrStartForAssembly(...).Dispatch):\n" + string.Join('\n', offenders));
    }

    // The simple type name after each "new " on the line: `new TickBar { ... }` gives TickBar.
    private static IEnumerable<string> NewTypeNames(string line)
    {
        for (var at = line.IndexOf("new ", StringComparison.Ordinal); at >= 0; at = line.IndexOf("new ", at + 4, StringComparison.Ordinal))
        {
            var start = at + 4;
            var end = start;
            while (end < line.Length && (char.IsLetterOrDigit(line[end]) || line[end] == '_'))
            {
                end++;
            }

            if (end > start && char.IsUpper(line[start]) && (at == 0 || !char.IsLetterOrDigit(line[at - 1])))
            {
                yield return line[start..end];
            }
        }
    }

    // Every public Avalonia type and every type of the app's UI assembly that derives from AvaloniaObject, by simple name.
    private static HashSet<string> AvaloniaObjectNames() =>
        new[] { typeof(AvaloniaObject).Assembly, typeof(Control).Assembly, typeof(SindriPlatform).Assembly }
            .SelectMany(Exported)
            .Where(t => typeof(AvaloniaObject).IsAssignableFrom(t))
            .Select(t => t.Name)
            .ToHashSet(StringComparer.Ordinal);

    private static Type[] Exported(Assembly assembly)
    {
        try
        {
            return assembly.GetTypes();
        }
        catch (ReflectionTypeLoadException e)
        {
            return [.. e.Types.OfType<Type>()];
        }
    }
}
