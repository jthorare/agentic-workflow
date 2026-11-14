using System.Diagnostics.CodeAnalysis;
using System.Windows.Input;
using Avalonia;
using Avalonia.Controls.Primitives;
using Aw.Sindri.Ui.ViewModels.Rows;

namespace Aw.Sindri.Ui.Views.Controls.Rows;

/// <summary>
/// The 440 px pane beside a <see cref="RowList"/> (#2978): a 48 px header (title, id, open full, close), the fields in two columns of 28 px rows, the body, and the actions at
/// the bottom. It never navigates: open full and close are commands the page supplies. The template is in RowStyles.axaml.
/// </summary>
[SuppressMessage("Design", "MA0182", Justification = "Pages adopt it in their own PRs (#2978).")]
internal sealed class PeekPane : TemplatedControl
{
    /// <summary>The row's title, shown in the header.</summary>
    public static readonly StyledProperty<string?> TitleProperty = AvaloniaProperty.Register<PeekPane, string?>(nameof(Title));

    /// <summary>The row's display id, shown in mono after the title.</summary>
    public static readonly StyledProperty<string?> ItemIdProperty = AvaloniaProperty.Register<PeekPane, string?>(nameof(ItemId));

    /// <summary>The fields block, label and value pairs.</summary>
    public static readonly StyledProperty<IReadOnlyList<PeekField>?> FieldsProperty = AvaloniaProperty.Register<PeekPane, IReadOnlyList<PeekField>?>(nameof(Fields));

    /// <summary>The body: a thread, a diff summary, a description.</summary>
    public static readonly StyledProperty<object?> BodyProperty = AvaloniaProperty.Register<PeekPane, object?>(nameof(Body));

    /// <summary>The 2 or 3 buttons at the bottom, each with its key as a kbd inside; the main one is primary.</summary>
    public static readonly StyledProperty<object?> ActionsProperty = AvaloniaProperty.Register<PeekPane, object?>(nameof(Actions));

    /// <summary>What the open-full button runs.</summary>
    public static readonly StyledProperty<ICommand?> OpenFullCommandProperty = AvaloniaProperty.Register<PeekPane, ICommand?>(nameof(OpenFullCommand));

    /// <summary>What the close button runs.</summary>
    public static readonly StyledProperty<ICommand?> CloseCommandProperty = AvaloniaProperty.Register<PeekPane, ICommand?>(nameof(CloseCommand));

    /// <summary>The row's title, shown in the header.</summary>
    public string? Title
    {
        get => GetValue(TitleProperty);
        set => SetValue(TitleProperty, value);
    }

    /// <summary>The row's display id.</summary>
    public string? ItemId
    {
        get => GetValue(ItemIdProperty);
        set => SetValue(ItemIdProperty, value);
    }

    /// <summary>The fields block.</summary>
    public IReadOnlyList<PeekField>? Fields
    {
        get => GetValue(FieldsProperty);
        set => SetValue(FieldsProperty, value);
    }

    /// <summary>The body.</summary>
    public object? Body
    {
        get => GetValue(BodyProperty);
        set => SetValue(BodyProperty, value);
    }

    /// <summary>The buttons at the bottom.</summary>
    public object? Actions
    {
        get => GetValue(ActionsProperty);
        set => SetValue(ActionsProperty, value);
    }

    /// <summary>What the open-full button runs.</summary>
    public ICommand? OpenFullCommand
    {
        get => GetValue(OpenFullCommandProperty);
        set => SetValue(OpenFullCommandProperty, value);
    }

    /// <summary>What the close button runs.</summary>
    public ICommand? CloseCommand
    {
        get => GetValue(CloseCommandProperty);
        set => SetValue(CloseCommandProperty, value);
    }
}
