using Avalonia;
using Avalonia.Automation.Peers;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.VisualTree;
using Aw.Kernel.Testing;
using Aw.Sindri.Ui.Navigation;
using Aw.Sindri.Ui.Platform;
using Aw.Sindri.Ui.Testing;
using Aw.Sindri.Ui.Tests.Start;
using Aw.Sindri.Ui.Tests.Views;
using Aw.Sindri.Ui.ViewModels;
using Aw.Sindri.Ui.ViewModels.Instance;
using Aw.Sindri.Ui.ViewModels.Shell;
using Aw.Sindri.Ui.Views.Shell;
using Aw.Sindri.Ui.Views.Start;

namespace Aw.Sindri.Ui.Controls.Tests.Views;

/// <summary>
/// The Start form as a modal dialog (U5'): `N` and the buttons open it with focus on the recipe picker, Esc closes it, Ctrl+Enter sends
/// it, a refusal is read inline and takes focus, and every field is named by its visible words. Two key frames (frames/u5p-start-*.png).
/// </summary>
[Lane(TestLane.Integration)]
[Collection(HeadlessWindows.Name)]
public sealed class StartDialogTests : FrameHost
{
    private const string Prefix = "u5p-";

    private static void Frame(Window window, string name) => Frame(window, name, Prefix);

    private static Control? Focused(Window window) => window.FocusManager?.GetFocusedElement() as Control;

    private async Task<(ShellWindow Window, ShellViewModel Shell)> OnWorkflows(double width = 1400)
    {
        var (window, shell) = await Connected(width: width, height: 900, ready: s => s.Workflows.Groups.Count > 0 && s.Health.Catalog.Controls.Count > 0);
        shell.GoTo(PageId.Workflows);
        Settle(window);
        return (window, shell);
    }

    private static async Task Click(Window window, string name)
    {
        var button = Named<Button>(window, name);
        Assert.True(button.IsEffectivelyEnabled);
        button.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        await Task.Yield();
        Settle(window);
    }

    private static async Task Fill(Window window, ShellViewModel shell, string item, string value)
    {
        await Until(window, () => shell.Start.Recipes.Count > 0 && !shell.Start.IsLoading);
        await shell.Start.Choose(shell.Start.Recipes[0], Token);
        shell.Start.ItemId = item;
        shell.Start.Fields[0].Value = value;
        Settle(window);
    }

    [Fact]
    public Task N_and_the_two_buttons_open_the_form_with_focus_on_the_recipe_picker_and_Esc_closes_it() => OnUi(async () =>
    {
        var (window, shell) = await OnWorkflows();

        window.KeyPressQwerty(PhysicalKey.N, RawInputModifiers.None);
        await Until(window, () => shell.Start.IsOpen && Focused(window) == Named<ComboBox>(window, "StartRecipe"));
        window.KeyPressQwerty(PhysicalKey.Escape, RawInputModifiers.None);
        await Until(window, () => !shell.Start.IsOpen);
        await Click(window, "WorkflowsStart");
        await Until(window, () => shell.Start.IsOpen && Focused(window) == Named<ComboBox>(window, "StartRecipe"));
        shell.Start.Close();
        shell.GoTo(PageId.Home);
        Settle(window);
        await Click(window, "HomeStart");

        await Until(window, () => shell.Start.IsOpen && Focused(window) == Named<ComboBox>(window, "StartRecipe"));
        Assert.Equal("Start workflow", Named<Button>(window, "StartButton").GetVisualDescendants().OfType<TextBlock>().First().Text);
    });

    [Fact]
    public Task Ctrl_Enter_sends_the_form_and_the_instance_frame_opens() => OnUi(async () =>
    {
        var (window, shell) = await OnWorkflows();
        shell.OpenStart();
        await Fill(window, shell, StartGoldens.Request().Item, "the spec");
        Frame(window, "start-filled");

        window.KeyPress(Key.Enter, RawInputModifiers.Control, PhysicalKey.Enter, keySymbol: null);
        await Until(window, () => !shell.Start.IsOpen && shell.IsInstanceSheet);

        Assert.Equal(Wire(StartGoldens.Request()), Wire(Assert.Single(Server.Started)));
    });

    [Fact]
    public Task A_refusal_is_read_inline_and_takes_focus_and_the_form_stays_pressable() => OnUi(async () =>
    {
        var (window, shell) = await OnWorkflows();
        shell.OpenStart();
        await Fill(window, shell, StartGoldens.Request().Item, "the spec");
        Server.StartAnswer = RouteAnswer.Problem("workflow-instances", "start-missing-input");

        window.KeyPress(Key.Enter, RawInputModifiers.Control, PhysicalKey.Enter, keySymbol: null);
        await Until(window, () => shell.Start.HasError && Focused(window) == Named<Border>(window, "StartError"));

        Assert.Equal((true, true), (shell.Start.IsOpen, shell.Start.CanStart));
        Assert.Equal(shell.Start.Error, Heard.Name(Named<Border>(window, "StartError")));
        Frame(window, "start-refused");
    });

    // Every field is named by the words it shows, and the card is a window; walked the way an assistive client walks it.
    [Fact]
    public Task Every_field_is_heard_by_the_words_it_shows() => OnUi(async () =>
    {
        var (window, shell) = await OnWorkflows();
        shell.OpenStart();
        await Fill(window, shell, StartGoldens.Request().Item, string.Empty);

        var heard = Heard.Tree(Named<Control>(window, "StartCard")).Select(n => n.Name).ToList();

        Assert.Equal((AutomationControlType.Window, Labels.StartWorkflow), (ControlAutomationPeer.CreatePeerForElement(Named<Control>(window, "StartCard")).GetAutomationControlType(), Heard.Name(Named<Control>(window, "StartCard"))));
        Assert.Contains(Labels.StartRecipe, heard, StringComparer.Ordinal);
        Assert.Contains(Labels.StartItem, heard, StringComparer.Ordinal);
        Assert.All(shell.Start.Fields, f => Assert.Contains(f.Caption, heard, StringComparer.Ordinal));
        Assert.Contains(Labels.Cancel, heard, StringComparer.Ordinal);
        Assert.Contains(Labels.StartWorkflow, heard, StringComparer.Ordinal);
    });

    private static readonly RawInputModifiers _command = OperatingSystem.IsMacOS() ? RawInputModifiers.Meta : RawInputModifiers.Control;

    private static bool Within(Control? inner, Visual outer) => inner is not null && (ReferenceEquals(inner, outer) || inner.GetVisualAncestors().Contains(outer));

    // The list as the Start form reads it, with one more recipe: the one that has a file input.
    private void ListProject()
    {
        var list = System.Text.Json.Nodes.JsonNode.Parse(GoldenFiles.Api("workflow-recipes", "list"))!; // why: the golden is a JSON document
        var row = list["recipes"]![0]!.DeepClone(); // why: the golden has a row
        (row["id"], row["version"]) = (StartFormFixture.Project, 12);
        list["recipes"]!.AsArray().Add(row); // why: the golden has a list
        Server.RecipeListAnswer = new RouteAnswer(System.Net.HttpStatusCode.OK, list.ToJsonString());
    }

    // Ctrl (⌘ on macOS) + N opens the form from any page, and Esc closes it again.
    [Fact]
    public Task The_command_chord_N_opens_the_form_from_Home_and_from_Workflows() => OnUi(async () =>
    {
        var (window, shell) = await Connected(width: 1400, height: 900, ready: s => s.Health.Catalog.Controls.Count > 0);

        window.KeyPress(Key.N, _command, PhysicalKey.N, "n");
        await Until(window, () => shell.Start.IsOpen && Focused(window) == Named<ComboBox>(window, "StartRecipe"));
        shell.Start.Close();
        shell.GoTo(PageId.Workflows);
        Settle(window);
        window.KeyPress(Key.N, _command, PhysicalKey.N, "n");

        await Until(window, () => shell.Start.IsOpen && Focused(window) == Named<ComboBox>(window, "StartRecipe"));
    });

    // Choosing a recipe through the picker (not the view model) reads its inputs and draws a field for each.
    [Fact]
    public Task Choosing_a_recipe_in_the_picker_draws_its_fields() => OnUi(async () =>
    {
        var (window, shell) = await OnWorkflows();
        shell.OpenStart();
        await Until(window, () => shell.Start.Recipes.Count > 0 && !shell.Start.IsLoading);

        Named<ComboBox>(window, "StartRecipe").SelectedItem = shell.Start.Recipes[0];

        await Until(window, () => shell.Start.Fields.Count > 0 && shell.Start.Recipe == shell.Start.Recipes[0]);
        Assert.Contains(shell.Start.Fields[0].Caption, Heard.Tree(Named<Control>(window, "StartCard")).Select(n => n.Name), StringComparer.Ordinal);
    });

    // The file button is named by what it chooses and what is chosen; a click asks the picker, and the name follows the choice.
    [Fact]
    public Task The_file_button_is_named_by_its_input_and_its_file_and_asks_the_picker() => OnUi(async () =>
    {
        ListProject();
        var (window, shell) = await OnWorkflows();
        shell.OpenStart();
        await Until(window, () => shell.Start.Recipes.Count > 0 && !shell.Start.IsLoading);
        await shell.Start.Choose(shell.Start.Recipes.Single(r => r.Id == StartFormFixture.Project), Token);
        Settle(window);
        var field = shell.Start.Fields.First(f => f.IsFile);
        var button = Named<StartDialog>(window, "StartDialog").GetVisualDescendants().OfType<Button>().First(b => b.Classes.Contains("start-pick") && ReferenceEquals(b.DataContext, field));
        Assert.Equal(Labels.StartFileButtonName(field.Caption, Labels.NoFileChosen), Heard.Name(button));
        Files.Queue(new PickedFile("prd.md", 3, new byte[] { 1, 2, 3 }));

        button.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        await Until(window, () => field.FileLine.StartsWith("prd.md", StringComparison.Ordinal));

        Assert.Equal((1, Labels.StartFileButtonName(field.Caption, field.FileLine)), (Files.Asked.Count, Heard.Name(button)));
        Assert.Contains("prd.md", Heard.Name(button), StringComparison.Ordinal);
    });

    // A finished attempt offers a new one: the button sends the next attempt for the item and recipe that were shown.
    [Fact]
    public Task The_new_attempt_button_sends_the_next_attempt_after_a_finished_answer() => OnUi(async () =>
    {
        var (window, shell) = await OnWorkflows();
        shell.OpenStart();
        await Fill(window, shell, StartGoldens.Request().Item, "the spec");
        var finished = System.Text.Json.Nodes.JsonNode.Parse(GoldenFiles.Api("workflow-instances-start", "started"))!; // why: the golden is a JSON document
        (finished["existing"], finished["finished"]) = (true, true);
        Server.StartAnswer = new RouteAnswer(System.Net.HttpStatusCode.OK, finished.ToJsonString());
        window.KeyPress(Key.Enter, RawInputModifiers.Control, PhysicalKey.Enter, keySymbol: null);
        await Until(window, () => shell.Start.OffersNewAttempt);

        await Click(window, "StartNewAttempt");
        await Until(window, () => Server.Started.Count == 2);

        Assert.Equal((false, true), (Server.Started[0].NextAttempt, Server.Started[1].NextAttempt));
    });

    // An existing attempt is answered in the form: focus stays inside it (the frame behind the scrim does not take it), and the words are spoken once.
    [Fact]
    public Task An_existing_answer_keeps_focus_in_the_form_and_announces_the_outcome() => OnUi(async () =>
    {
        var (window, shell) = await OnWorkflows();
        shell.OpenStart();
        await Fill(window, shell, StartGoldens.Request().Item, "the spec");
        var existing = System.Text.Json.Nodes.JsonNode.Parse(GoldenFiles.Api("workflow-instances-start", "started"))!; // why: the golden is a JSON document
        existing["existing"] = true;
        Server.StartAnswer = new RouteAnswer(System.Net.HttpStatusCode.OK, existing.ToJsonString());

        window.KeyPress(Key.Enter, RawInputModifiers.Control, PhysicalKey.Enter, keySymbol: null);
        await Until(window, () => shell.Start.HasOutcome && shell.IsInstanceSheet && shell.Instance.Phase is not (FramePhase.Loading or FramePhase.Empty));
        Settle(window);

        var focused = Focused(window);
        Assert.True(Within(focused, Named<Control>(window, "StartCard")), $"focus is on {focused?.GetType().Name} {focused?.Name} (modal open: {shell.Start.IsOpen})");
        var live = Named<TextBlock>(window, "StartLive");
        Assert.Equal((shell.Start.Outcome, Avalonia.Automation.AutomationProperties.GetLiveSetting(live)), (live.Text, Avalonia.Automation.AutomationLiveSetting.Polite));
        Assert.Equal(Labels.StartWhySent, Avalonia.Automation.AutomationProperties.GetHelpText(Named<Button>(window, "StartButton")));
    });

    // A new instance's frame takes focus once the form has closed, and focus never visits what opened the form on the way (a screen reader would read it);
    // below 1100 px the frame covers the page, above it it sits beside it.
    [Theory]
    [InlineData(1400)]
    [InlineData(1000)]
    public Task An_accepted_start_moves_focus_to_the_new_frame_without_visiting_the_opener(int width) => OnUi(async () =>
    {
        var (window, shell) = await OnWorkflows(width);
        var opener = Named<Button>(window, "WorkflowsStart");
        _ = opener.Focus(NavigationMethod.Tab);
        shell.OpenStart();
        await Fill(window, shell, StartGoldens.Request().Item, "the spec");
        var visits = 0;
        opener.GotFocus += (_, _) => visits++;

        window.KeyPress(Key.Enter, RawInputModifiers.Control, PhysicalKey.Enter, keySymbol: null);
        await Until(window, () => !shell.Start.IsOpen && shell.IsInstanceSheet);
        await Until(window, () => Within(Focused(window), Named<Control>(window, "Sheet")));

        Assert.Equal(0, visits);
    });

    // A field that cannot be sent takes focus itself and carries its sentence as help text; the error line is not said twice.
    [Fact]
    public Task A_refused_field_takes_focus_and_carries_its_sentence_as_help_text() => OnUi(async () =>
    {
        var (window, shell) = await OnWorkflows();
        shell.OpenStart();
        await Fill(window, shell, StartGoldens.Request().Item, string.Empty);

        window.KeyPress(Key.Enter, RawInputModifiers.Control, PhysicalKey.Enter, keySymbol: null);
        await Until(window, () => shell.Start.HasError && Focused(window) is TextBox);

        var focused = Focused(window)!; // why: the Until above found it
        Assert.Equal((shell.Start.Fields[0].Error, Labels.StartRequired(shell.Start.Fields[0].Label)), (Avalonia.Automation.AutomationProperties.GetHelpText(focused), shell.Start.ErrorDetail));
        Assert.False(Named<Border>(window, "StartError").IsVisible);
    });

    // No item chosen: focus goes to the item box, which carries the sentence as help text.
    [Fact]
    public Task A_missing_item_takes_focus_to_the_item_box() => OnUi(async () =>
    {
        var (window, shell) = await OnWorkflows();
        shell.OpenStart();
        await Fill(window, shell, string.Empty, "the spec");

        window.KeyPress(Key.Enter, RawInputModifiers.Control, PhysicalKey.Enter, keySymbol: null);
        await Until(window, () => shell.Start.HasError && Focused(window) == Named<TextBox>(window, "StartItem"));

        Assert.Equal(Labels.StartChooseItem, Avalonia.Automation.AutomationProperties.GetHelpText(Named<TextBox>(window, "StartItem")));
    });

    private async Task<(ShellWindow Window, ShellViewModel Shell)> FilledForExisting(bool finished)
    {
        var (window, shell) = await OnWorkflows();
        shell.OpenStart();
        await Fill(window, shell, StartGoldens.Request().Item, "the spec");
        var answer = System.Text.Json.Nodes.JsonNode.Parse(GoldenFiles.Api("workflow-instances-start", "started"))!; // why: the golden is a JSON document
        (answer["existing"], answer["finished"]) = (true, finished);
        Server.StartAnswer = new RouteAnswer(System.Net.HttpStatusCode.OK, answer.ToJsonString());
        return (window, shell);
    }

    private static async Task Press(Window window, ShellViewModel shell, Func<bool> done)
    {
        var button = Named<Button>(window, "StartButton");
        _ = button.Focus(NavigationMethod.Tab);
        button.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        await Until(window, () => done() && shell.IsInstanceSheet && shell.Instance.Phase is not (FramePhase.Loading or FramePhase.Empty));
        Settle(window);
    }

    // Pressing the Start button disables it; focus must not be left nowhere (the next Tab would leave the form). After a finished attempt it goes to the new-attempt button.
    [Fact]
    public Task Pressing_Start_after_a_finished_answer_leaves_focus_on_the_new_attempt_button() => OnUi(async () =>
    {
        var (window, shell) = await FilledForExisting(finished: true);

        await Press(window, shell, () => shell.Start.OffersNewAttempt);

        Assert.Same(Named<Button>(window, "StartNewAttempt"), Focused(window));
    });

    [Fact]
    public Task Pressing_Start_after_an_already_running_answer_leaves_focus_on_the_outcome_line() => OnUi(async () =>
    {
        var (window, shell) = await FilledForExisting(finished: false);

        await Press(window, shell, () => shell.Start.HasOutcome);

        Assert.Same(Named<Border>(window, "StartOutcome"), Focused(window));
    });

    // The frame an existing answer opened behind the form gets focus when the form closes, not the opener.
    [Fact]
    public Task Closing_the_form_after_an_existing_answer_hands_focus_to_the_frame_behind_it() => OnUi(async () =>
    {
        var (window, shell) = await FilledForExisting(finished: false);
        await Press(window, shell, () => shell.Start.HasOutcome);

        window.KeyPressQwerty(PhysicalKey.Escape, RawInputModifiers.None);
        await Until(window, () => !shell.Start.IsOpen && Within(Focused(window), Named<Control>(window, "Sheet")));
    });

    // The polite region rests, speaks a refusal, rests again, and speaks the same refusal a second time; a reopened form starts at rest.
    [Fact]
    public Task The_live_region_is_in_the_tree_at_rest_says_a_repeated_refusal_again_and_starts_each_opening_clean() => OnUi(async () =>
    {
        var (window, shell) = await OnWorkflows();
        shell.OpenStart();
        await Fill(window, shell, StartGoldens.Request().Item, string.Empty);
        var live = Named<TextBlock>(window, "StartLive");
        Assert.True(live.IsVisible);
        var said = new List<(string? Text, Avalonia.Automation.AutomationLiveSetting Live)>();
        _ = live.GetObservable(TextBlock.TextProperty).Subscribe(new Observer<string?>(text => said.Add((text, Avalonia.Automation.AutomationProperties.GetLiveSetting(live)))));
        var sentence = Labels.StartRequired(shell.Start.Fields[0].Label);

        window.KeyPress(Key.Enter, RawInputModifiers.Control, PhysicalKey.Enter, keySymbol: null);
        await Until(window, () => shell.Start.HasError);
        window.KeyPress(Key.Enter, RawInputModifiers.Control, PhysicalKey.Enter, keySymbol: null);
        Settle(window);

        Assert.Equal(2, said.Count(s => string.Equals(s.Text, sentence, StringComparison.Ordinal) && s.Live == Avalonia.Automation.AutomationLiveSetting.Polite));
        Assert.DoesNotContain(said, s => string.Equals(s.Text, sentence, StringComparison.Ordinal) && s.Live == Avalonia.Automation.AutomationLiveSetting.Off);
        shell.Start.Close();
        shell.OpenStart();
        await Until(window, () => shell.Start.IsOpen && !shell.Start.IsLoading);
        Assert.NotEqual(sentence, live.Text, StringComparer.Ordinal);
    });

    private sealed class Observer<T>(Action<T> next) : IObserver<T>
    {
        public void OnCompleted() { }

        public void OnError(Exception error) { }

        public void OnNext(T value) => next(value);
    }

    // Picking a recipe again after its read failed is a change of selection, so the picker reads it again.
    [Fact]
    public Task A_recipe_whose_read_failed_can_be_picked_again_in_the_picker() => OnUi(async () =>
    {
        var (window, shell) = await OnWorkflows();
        shell.OpenStart();
        await Until(window, () => shell.Start.Recipes.Count > 0 && !shell.Start.IsLoading);
        var good = Server.WorkflowAnswer;
        Server.WorkflowAnswer = RouteAnswer.Problem("workflow-recipe", "not-found");
        var picker = Named<ComboBox>(window, "StartRecipe");
        picker.SelectedItem = shell.Start.Recipes[0];
        await Until(window, () => shell.Start.HasNote && picker.SelectedItem is null);

        Server.WorkflowAnswer = good;
        picker.SelectedItem = shell.Start.Recipes[0];

        await Until(window, () => shell.Start.Fields.Count > 0 && shell.Start.CanStart);
    });

    // No recipe chosen: focus goes to the recipe picker. An empty required file: to its button.
    [Fact]
    public Task A_missing_recipe_takes_focus_to_the_picker() => OnUi(async () =>
    {
        var (window, shell) = await OnWorkflows();
        shell.OpenStart();
        await Until(window, () => shell.Start.Recipes.Count > 0 && !shell.Start.IsLoading);
        shell.Start.ItemId = StartGoldens.Request().Item;

        window.KeyPress(Key.Enter, RawInputModifiers.Control, PhysicalKey.Enter, keySymbol: null);

        await Until(window, () => shell.Start.HasError && Focused(window) == Named<ComboBox>(window, "StartRecipe"));
        Assert.Equal(Labels.StartChooseRecipe, Avalonia.Automation.AutomationProperties.GetHelpText(Named<ComboBox>(window, "StartRecipe")));
    });

    [Fact]
    public Task An_empty_required_file_takes_focus_to_its_button() => OnUi(async () =>
    {
        ListProject();
        var (window, shell) = await OnWorkflows();
        shell.OpenStart();
        await Until(window, () => shell.Start.Recipes.Count > 0 && !shell.Start.IsLoading);
        await shell.Start.Choose(shell.Start.Recipes.Single(r => r.Id == StartFormFixture.Project), Token);
        shell.Start.ItemId = StartGoldens.Request().Item;
        shell.Start.Fields.Single(f => f.IsPayload).Value = "{}";
        Settle(window);

        window.KeyPress(Key.Enter, RawInputModifiers.Control, PhysicalKey.Enter, keySymbol: null);

        await Until(window, () => shell.Start.HasError && Focused(window) is Button b && b.Classes.Contains("start-pick"));
    });

    // The chord opens the form over an open palette (one layer at a time), the palette's chord is swallowed under the form, and the scrim closes it.
    [Fact]
    public Task The_start_chord_replaces_an_open_palette_and_the_palette_chord_is_swallowed_under_the_form() => OnUi(async () =>
    {
        var (window, shell) = await OnWorkflows();
        window.KeyPress(Key.K, _command, PhysicalKey.K, "k");
        await Until(window, () => shell.Palette.IsOpen);

        window.KeyPress(Key.N, _command, PhysicalKey.N, "n");
        await Until(window, () => shell.Start.IsOpen && !shell.Palette.IsOpen);
        window.KeyPress(Key.K, _command, PhysicalKey.K, "k");
        Settle(window);

        Assert.Equal((true, false), (shell.Start.IsOpen, shell.Palette.IsOpen));
    });

    [Fact]
    public Task A_click_on_the_scrim_closes_the_form() => OnUi(async () =>
    {
        var (window, shell) = await OnWorkflows();
        shell.OpenStart();
        await Until(window, () => shell.Start.IsOpen);
        var outside = new Point(40, 700);

        window.MouseDown(outside, MouseButton.Left);
        window.MouseUp(outside, MouseButton.Left);

        await Until(window, () => !shell.Start.IsOpen);
    });

    private static string Wire(Api.Workflow.StartInstanceRequest request) =>
        System.Text.Encoding.UTF8.GetString(Kernel.Json.AwJson.Serialize(request, Api.SindriApiClient.Json));
}
