using System.Diagnostics.CodeAnalysis;
using Aw.Sindri.Ui.Navigation;
using Aw.Sindri.Ui.ViewModels.Toast;
using Aw.Sindri.Ui.ViewModels.Todo;

namespace Aw.Sindri.Ui.ViewModels.Shell;

/// <summary>The Todo panel in the shell (<see cref="ShellViewModel"/>, continued; todo spec §5): it reads on connect and after every event, first among Home's reads (D12).</summary>
internal sealed partial class ShellViewModel
{
    // A pick's Undo window (todo spec D11): 10 s, counted down in its toast, which no newer toast replaces.
    private const int PickWindowSeconds = 10;

    /// <summary>The Todo panel at the top of Home.</summary>
    public TodoPanelViewModel Todo { get; private set; }

    /// <summary>False while Home's decisions filter (G D) is on: the panel is hidden then.</summary>
    public bool IsTodoShown => HomeFilter != HomeFilter.Decisions;

    /// <summary>True while focus is inside Home's Needs you panel (the other focus region, spec D6); the view sets it.</summary>
    public bool IsNeedsYouFocused { get; internal set => Set(ref field, value); }

    [MemberNotNull(nameof(Todo))]
    private void InitTodo()
    {
        Todo = new TodoPanelViewModel(_api, _clock);
        Health.Follow(Todo.Refresh);
        Health.PropertyChanged += (_, e) =>
        {
            if (string.Equals(e.PropertyName, nameof(HealthViewModel.IsProfileMissing), StringComparison.Ordinal))
            {
                Todo.SetProfileMissing(Health.IsProfileMissing);
                Elsewhere.SetProfileMissing(Health.IsProfileMissing);
            }
        };
        Todo.LinkOpened += (_, link) => OpenTodoLink(link.Href);
        Todo.Acted += (_, acted) => ShowToast(new ToastMessage(acted.Text, acted.Undo));
        Todo.Picked += (_, picked) => ShowToast(new ToastMessage(picked.Text, picked.Undo, ToastKind.Pick, picked.Seconds ?? PickWindowSeconds));
    }

    // A todo's link goes where the same link goes anywhere in the app ("sindri://linear/ENG-12" is the deep link "linear/ENG-12"); a link no page opens does nothing.
    private void OpenTodoLink(string href)
    {
        const string scheme = "sindri://";
        if (DeepLink.TryParse(href.StartsWith(scheme, StringComparison.Ordinal) ? href[scheme.Length..] : href, out var link))
        {
            Navigate(link);
        }
    }

    // The keys Needs you acts on (J, K, A, X, Y, Enter) belong to the panel alone while focus is inside it (D6): the shell spends them, so X there can never reject a decision.
    private static readonly HashSet<string> _panelKeys = new(["J", "K", "A", "X", "Y", "Enter", "O"], StringComparer.Ordinal);

    private bool TodoOwns(string key, bool inSheet) => Todo.HasFocus && !inSheet && IsHomeShown && _panelKeys.Contains(key);
}
