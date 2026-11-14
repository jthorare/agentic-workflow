using System.Net;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Aw.Kernel.Testing;
using Aw.Sindri.Ui.Navigation;
using Aw.Sindri.Ui.Testing;
using Aw.Sindri.Ui.Tests.Views;
using Aw.Sindri.Ui.ViewModels;
using Aw.Sindri.Ui.ViewModels.Shell;
using Aw.Sindri.Ui.Views.Shell;
using Microsoft.Extensions.Time.Testing;

namespace Aw.Sindri.Ui.Frames.Tests.Views;

/// <summary>The concepts line on the Context page (#2552): a failed concepts read says so in plain words, and its error code reaches the line's tooltip on screen.</summary>
[Lane(TestLane.Integration)]
public sealed class ContextConceptsNoteWindowTests
{
    private const string Code = "SND-CONTEXT-041";

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    [Fact]
    public async Task A_failed_concepts_read_puts_its_code_in_the_concepts_line_tooltip()
    {
        using var stop = CancellationTokenSource.CreateLinkedTokenSource(Token);
        _ = await HeadlessUnitTestSession.GetOrStartForAssembly(typeof(ContextConceptsNoteWindowTests).Assembly).Dispatch(async () =>
        {
            var records = new InMemoryEndpointRecords();
            records.Write(A.Record());
            var api = new FakeSindriApi { ContextConceptsAnswer = _ => RouteAnswer.Problem(HttpStatusCode.InternalServerError, Code, "the concepts can't be read") };
            var shell = new ShellViewModel(records, api, new FakeTimeProvider(A.Noon), new Preferences(new InMemoryPreferencesStore()), new FakeClipboard());
            await shell.Restore(Token);
            var window = new ShellWindow { DataContext = shell, Width = 1280, Height = 800 };
            try
            {
                window.Show();
                _ = shell.Run(stop.Token);
                for (var i = 0; i < 2000 && shell.State != ShellState.Normal; i++)
                {
                    await Task.Delay(1, Token);
                    Dispatcher.UIThread.RunJobs();
                }

                shell.GoTo(PageId.Context);
                var note = await NoteWithWords(window);

                Assert.NotNull(note);
                Assert.Equal("Concepts can't be read right now, showing the whole map.", note.Text);
                Assert.Equal(Code, ToolTip.GetTip(note));
                SaveFrames(window, note);
            }
            finally
            {
                await stop.CancelAsync();
                window.Close();
                Dispatcher.UIThread.RunJobs();
            }

            return true;
        }, Token);
    }

    // The page with the failed-read line, then with its tooltip open (frames/c2552-*.png under AW_UI_FRAMES=1).
    private static void SaveFrames(ShellWindow window, TextBlock note)
    {
        FrameFile.Save(window, "c2552-concepts-read-failed");
        ToolTip.SetIsOpen(note, value: true);
        Dispatcher.UIThread.RunJobs();
        FrameFile.Save(window, "c2552-concepts-read-failed-tooltip");
        ToolTip.SetIsOpen(note, value: false);
    }

    // The concepts line once it has words (the read is answered on the dispatcher), or null after two seconds.
    private static async Task<TextBlock?> NoteWithWords(ShellWindow window)
    {
        TextBlock? note = null;
        for (var i = 0; i < 2000 && (note is null || note.Text is not { Length: > 0 }); i++)
        {
            await Task.Delay(1, Token);
            Dispatcher.UIThread.RunJobs();
            window.UpdateLayout();
            note = window.GetVisualDescendants().OfType<TextBlock>().FirstOrDefault(t => t.Name == "ConceptsNote");
        }

        return note;
    }
}
