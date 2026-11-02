using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.VisualTree;
using Aw.Kernel.Testing;
using Aw.Sindri.Ui.Navigation;
using Aw.Sindri.Ui.Tests.Views;

namespace Aw.Sindri.Ui.Tests.Projects;

/// <summary>The notes area in the card: Enter is a newline there and a key or click inside it never reaches the card around it.</summary>
[Lane(TestLane.Integration)]
[Collection(HeadlessWindows.Name)]
public sealed class ProjectNotesViewTests : FrameHost
{
    [Fact]
    public Task Enter_and_a_press_inside_the_notes_box_stop_at_the_notes_area() => OnUi(async () =>
    {
        var (window, shell) = await Connected();
        shell.GoTo(PageId.Projects);
        await Until(window, () => !shell.Projects.IsLoading, "the Projects page never read the list");
        var card = shell.Projects.Groups[0].Cards[0];
        card.BeginEdit();
        var frame = window.GetVisualDescendants().OfType<Border>().First(b => b.Name == "ProjectCardFrame" && b.DataContext == card);
        var seen = new List<string>();
        frame.AddHandler(InputElement.KeyDownEvent, (_, _) => seen.Add("key"), RoutingStrategies.Bubble);
        frame.AddHandler(InputElement.PointerPressedEvent, (_, _) => seen.Add("press"), RoutingStrategies.Bubble);
        await Until(window, () => frame.GetVisualDescendants().OfType<TextBox>().Any(t => t.Name == "ProjectNotesBox" && t.IsEffectivelyVisible), "the notes box never showed");
        var box = frame.GetVisualDescendants().OfType<TextBox>().First(t => t.Name == "ProjectNotesBox");

        box.RaiseEvent(new KeyEventArgs { RoutedEvent = InputElement.KeyDownEvent, Key = Key.Enter, Source = box });
        box.RaiseEvent(new KeyEventArgs { RoutedEvent = InputElement.KeyDownEvent, Key = Key.Space, Source = box });
        box.RaiseEvent(new PointerPressedEventArgs(box, new Pointer(0, PointerType.Mouse, isPrimary: true), box, default, 0, new PointerPointProperties(RawInputModifiers.LeftMouseButton, PointerUpdateKind.LeftButtonPressed), KeyModifiers.None));

        Assert.Empty(seen);
    });
}
