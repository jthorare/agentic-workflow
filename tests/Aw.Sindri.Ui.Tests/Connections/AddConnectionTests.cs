using System.Reflection;
using Aw.Kernel;
using Aw.Kernel.Testing;
using Aw.Sindri.Ui.Api;
using Aw.Sindri.Ui.ViewModels;
using Aw.Sindri.Ui.ViewModels.Connections;

namespace Aw.Sindri.Ui.Tests.Connections;

/// <summary>The Add connection form: an OAuth preset opens the consent page and waits, a key goes out once and is gone.</summary>
[Lane(TestLane.Unit)]
public sealed class AddConnectionTests
{
    private const string Key = "sk-test-9f3a7c21d4";

    private readonly ConnectionsFixture _f = new();

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Adding_an_oauth_connection_opens_the_consent_URL_and_polls_until_connected()
    {
        var page = await _f.Shown();
        var form = page.Add;
        form.Open();
        form.SelectedPreset = form.Presets.Single(p => p.Id == "linear");
        form.Name = "linear:work";
        form.ClientId = "client-123";

        await _f.Drive(form.Send(Token), "linear:work");

        Assert.Equal([_f.Server.ConsentUrl], _f.Launcher.Opened, StringComparer.Ordinal);
        Assert.Equal(("linear:work", "linear", "client-123", false), (_f.Server.LastAddConnection!.Name, _f.Server.LastAddConnection.Preset, _f.Server.LastAddConnection.Client, _f.Server.LastAddConnection.Wait));
        Assert.Equal(Labels.ConnectionAdded("linear:work"), form.StatusText);
        Assert.Equal(["✓ Connected"], page.Rows.Select(r => r.ChipText), StringComparer.Ordinal);
        Assert.Contains("get linear:work", _f.Server.ConnectionCallLog, StringComparer.Ordinal);
    }

    [Fact]
    public async Task A_new_oauth_add_keeps_waiting_while_no_row_exists_and_shows_the_row_once_the_callback_lands()
    {
        var page = await _f.Shown();
        var form = OAuthForm(page);

        var run = form.Send(Token);
        await _f.Drive(Task.Delay(30, Token));   // a minute of polls: the server has no row for linear:work yet

        Assert.False(run.IsCompleted);
        Assert.Equal((string.Empty, true, true), (form.ErrorText, form.HasStatus, form.IsWorking));
        Assert.Empty(page.Rows);

        _f.Server.CompleteSignIn("linear:work");
        await _f.Drive(run);

        Assert.Equal((Labels.ConnectionAdded("linear:work"), string.Empty), (form.StatusText, form.ErrorText));
        Assert.Equal(["linear:work"], page.Rows.Select(r => r.Name), StringComparer.Ordinal);
    }

    [Fact]
    public async Task One_dropped_read_during_the_wait_does_not_end_it()
    {
        var page = await _f.Shown();
        var form = OAuthForm(page);
        _f.Server.NextConnectionGetError = SindriUiErrors.Unreachable.With("nothing answered");

        await _f.Drive(form.Send(Token), "linear:work");

        Assert.Equal((Labels.ConnectionAdded("linear:work"), string.Empty), (form.StatusText, form.ErrorText));
    }

    [Fact]
    public async Task Any_other_read_error_ends_the_wait_with_that_error()
    {
        var page = await _f.Shown();
        var form = OAuthForm(page);
        _f.Server.NextConnectionGetError = new Error("SND-CONN-003", ErrorKind.Unavailable, "The provider refused the sign-in.");

        await _f.Drive(form.Send(Token));

        Assert.Contains("SND-CONN-003", form.ErrorText, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Closing_the_form_stops_the_wait_and_a_reopened_form_shows_nothing_from_it()
    {
        var page = await _f.Shown();
        var form = OAuthForm(page);

        var run = form.Send(Token);
        await _f.Drive(Task.Delay(10, Token));
        form.Close();
        await _f.Drive(run);   // ends without a callback
        form.Open();

        Assert.Equal((string.Empty, string.Empty, false), (form.StatusText, form.ErrorText, form.IsWorking));
    }

    [Fact]
    public async Task A_refused_oauth_add_clears_the_client_secret_too()
    {
        var page = await _f.Shown();
        var form = OAuthForm(page);
        form.ClientSecret.Text = Key;
        _f.Server.ConnectionWriteError = new Error("SND-CONN-010", ErrorKind.Conflict, "A connection named linear:work already exists.");

        await form.Send(Token);

        Assert.Equal(Key, Assert.Single(_f.Server.AddedConnections).ClientSecret);
        Assert.False(form.ClientSecret.HasValue);
        Assert.Empty(StringsHolding(Key, form, page));
    }

    [Fact]
    public async Task The_form_shows_the_fields_the_chosen_kind_needs()
    {
        var form = (await _f.Shown()).Add;

        form.SelectedPreset = form.Presets.Single(p => p.Id == "github");
        Assert.Equal((true, true, false, false), (form.ShowClient, form.ShowClientSecret, form.ShowKey, form.ShowUrl));

        form.SelectedPreset = form.Presets.Single(p => p.Id == "rest");
        Assert.Equal((false, false, true, false), (form.ShowClient, form.ShowClientSecret, form.ShowKey, form.ShowUrl));

        form.SelectedPreset = form.Presets.Single(p => p.Id == "mcp");
        Assert.Equal((false, false, false, true), (form.ShowClient, form.ShowClientSecret, form.ShowKey, form.ShowUrl));
    }

    [Fact]
    public async Task A_pasted_key_is_sent_once_and_never_reaches_preferences_logs_or_view_model_state()
    {
        var page = await _f.Shown();
        var form = page.Add;
        form.Open();
        form.SelectedPreset = form.Presets.Single(p => p.Id == "rest");
        form.Name = "acme:key";
        form.Key.Text = Key;

        await form.Send(Token);

        Assert.Equal(Key, Assert.Single(_f.Server.AddedConnections).Key);   // the one request carried it
        Assert.DoesNotContain(Key, _f.Server.LastAddConnection!.ToString(), StringComparison.Ordinal);
        Assert.Equal(string.Empty, form.Key.Text);
        Assert.False(form.Key.HasValue);
        Assert.Empty(StringsHolding(Key, form, page));
        Assert.Equal(["✓ Connected"], page.Rows.Select(r => r.ChipText), StringComparer.Ordinal);   // a key connection is connected at once
    }

    [Fact]
    public async Task A_refused_add_clears_the_secret_keeps_the_name_and_says_why()
    {
        var page = await _f.Shown();
        var form = page.Add;
        form.SelectedPreset = form.Presets.Single(p => p.Id == "rest");
        form.Name = "acme:key";
        form.Key.Text = Key;
        _f.Server.ConnectionWriteError = new Error("SND-CONN-010", ErrorKind.Conflict, "A connection named acme:key already exists.");

        await form.Send(Token);

        Assert.Equal(("acme:key", string.Empty), (form.Name, form.Key.Text));
        Assert.Contains("SND-CONN-010", form.ErrorText, StringComparison.Ordinal);
        Assert.Empty(StringsHolding(Key, form, page));
        Assert.Empty(page.Rows);
    }

    [Fact]
    public async Task Sending_needs_a_name_and_what_the_kind_requires()
    {
        var form = (await _f.Shown()).Add;
        form.SelectedPreset = form.Presets.Single(p => p.Id == "rest");

        Assert.False(form.CanSend);
        form.Name = "acme:key";
        Assert.False(form.CanSend);
        form.Key.Text = Key;
        Assert.True(form.CanSend);

        await form.Send(Token);
        _ = Assert.Single(_f.Server.AddedConnections);
    }

    private static AddConnectionViewModel OAuthForm(ConnectionsPageViewModel page)
    {
        var form = page.Add;
        form.Open();
        form.SelectedPreset = form.Presets.Single(p => p.Id == "linear");
        form.Name = "linear:work";
        form.ClientId = "client-123";
        return form;
    }

    // Every public string property, on the view models and on the view models they hold, that contains the text.
    private static List<string> StringsHolding(string text, params object[] roots)
    {
        var found = new List<string>();
        var seen = new HashSet<object>(ReferenceEqualityComparer.Instance);
        var queue = new Queue<object>(roots);
        while (queue.TryDequeue(out var next))
        {
            if (!seen.Add(next))
            {
                continue;
            }

            foreach (var (name, value) in PublicValues(next))
            {
                if (value is string s && s.Contains(text, StringComparison.Ordinal))
                {
                    found.Add(next.GetType().Name + "." + name);
                }

                foreach (var held in ViewModelsIn(value))
                {
                    queue.Enqueue(held);
                }
            }
        }

        return found;
    }

    private static IEnumerable<(string Name, object? Value)> PublicValues(object owner) =>
        owner.GetType().GetProperties(BindingFlags.Public | BindingFlags.Instance).Where(p => p.GetIndexParameters().Length == 0).Select(p => (p.Name, p.GetValue(owner)));

    private static IEnumerable<object> ViewModelsIn(object? value) => value switch
    {
        null or string => [],
        System.Collections.IEnumerable items => items.Cast<object?>().OfType<object>().Where(IsViewModel),
        _ => IsViewModel(value) ? [value] : [],
    };

    private static bool IsViewModel(object value) => value.GetType().Namespace?.StartsWith("Aw.Sindri.Ui.ViewModels", StringComparison.Ordinal) == true;
}
