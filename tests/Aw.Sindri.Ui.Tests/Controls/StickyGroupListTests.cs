using System.Collections;
using System.Globalization;
using System.Collections.ObjectModel;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Templates;
using Avalonia.Headless;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Aw.Kernel.Testing;
using Aw.Sindri.Ui.ViewModels;
using Aw.Sindri.Ui.Views.Controls;

namespace Aw.Sindri.Ui.Tests.Controls;

/// <summary>The sticky group list (needs-you spec §2): a group's header stays at the top while its rows are in view, the next header pushes it out, and a change touches nothing it need not.</summary>
[Lane(TestLane.Integration)]
public sealed class StickyGroupListTests
{
    private const double HeaderHeight = 32;
    private const double RowHeight = 40;

    private sealed class Group(string heading, int rows) : IRowGroup
    {
        internal string Heading { get; } = heading;

        internal ObservableCollection<string> Rows { get; } = [.. Enumerable.Range(0, rows).Select(i => heading + " " + i.ToString(CultureInfo.InvariantCulture))];

        IEnumerable IRowGroup.Items => Rows;
    }

    private static Task<T> OnUi<T>(Func<T> body) =>
        HeadlessUnitTestSession.GetOrStartForAssembly(typeof(StickyGroupListTests).Assembly).Dispatch(body, TestContext.Current.CancellationToken);

    private static (Window Window, StickyGroupList List, ObservableCollection<Group> Groups) Show(params Group[] groups)
    {
        var source = new ObservableCollection<Group>(groups);
        var list = new StickyGroupList
        {
            ItemsSource = source,
            HeaderTemplate = new FuncDataTemplate<Group>((g, _) => new Border { Height = HeaderHeight, Background = Avalonia.Media.Brushes.Gray, Child = new TextBlock { Text = g.Heading } }),
            ItemTemplate = new FuncDataTemplate<string>((s, _) => new TextBlock { Text = s, Height = RowHeight }),
        };
        var window = new Window { Content = list, Width = 400, Height = 300 };
        window.Show();
        Settle(window);
        return (window, list, source);
    }

    private static void Settle(Window window)
    {
        Dispatcher.UIThread.RunJobs();
        window.UpdateLayout();
        Dispatcher.UIThread.RunJobs();
    }

    private static string[] Texts(StickyGroupList list) => [.. list.GetVisualDescendants().OfType<TextBlock>().Select(t => t.Text ?? "")];

    [Fact]
    public async Task At_the_top_no_header_is_stuck()
    {
        var stuck = await OnUi(() =>
        {
            var (window, list, _) = Show(new Group("A", 6), new Group("B", 6));
            try
            {
                return list.StickyGroup;
            }
            finally
            {
                window.Close();
            }
        });

        Assert.Null(stuck);
    }

    [Fact]
    public async Task Scrolled_into_a_group_its_header_sticks()
    {
        var heading = await OnUi(() =>
        {
            var (window, list, groups) = Show(new Group("A", 6), new Group("B", 6), new Group("C", 6));
            try
            {
                var secondTop = HeaderHeight + (6 * RowHeight);
                list.ScrollTo(secondTop + 10);
                Settle(window);
                return ((Group?)list.StickyGroup)?.Heading + "/" + groups.Count.ToString(CultureInfo.InvariantCulture);
            }
            finally
            {
                window.Close();
            }
        });

        Assert.Equal("B/3", heading);
    }

    [Fact]
    public async Task The_next_header_pushes_the_stuck_one_out_as_it_arrives()
    {
        var (stickyPush, stuckHeading) = await OnUi(() =>
        {
            var (window, list, _) = Show(new Group("A", 6), new Group("B", 6));
            try
            {
                var secondTop = HeaderHeight + (6 * RowHeight);
                list.ScrollTo(secondTop - (HeaderHeight / 2));   // B's header is half a header from the top: A's is half out
                Settle(window);
                return (list.StickyPush, ((Group?)list.StickyGroup)?.Heading);
            }
            finally
            {
                window.Close();
            }
        });

        Assert.Equal("A", stuckHeading);
        Assert.InRange(stickyPush, -HeaderHeight, -1);
    }

    [Fact]
    public async Task A_row_that_arrives_changes_neither_the_other_groups_nor_the_stuck_header()
    {
        var result = await OnUi(() =>
        {
            var (window, list, groups) = Show(new Group("A", 6), new Group("B", 6));
            try
            {
                list.ScrollTo(HeaderHeight + (6 * RowHeight) + 10);
                Settle(window);
                var before = list.StickyGroup;
                var rowsOfA = Texts(list).Count(t => t.StartsWith('A'));

                groups[1].Rows.Add("B new");
                Settle(window);

                var rowsOfAAfter = Texts(list).Count(t => t.StartsWith('A'));
                return (ReferenceEquals(before, list.StickyGroup), rowsOfA == rowsOfAAfter, Texts(list).Contains("B new"));
            }
            finally
            {
                window.Close();
            }
        });

        Assert.Equal((true, true, true), result);
    }

    [Fact]
    public async Task A_group_that_goes_takes_its_rows_with_it_and_the_others_keep_their_controls()
    {
        var result = await OnUi(() =>
        {
            var (window, list, groups) = Show(new Group("A", 3), new Group("B", 3));
            try
            {
                var keptRow = list.GetVisualDescendants().OfType<TextBlock>().First(t => t.Text == "B 0");

                groups.RemoveAt(0);
                Settle(window);

                return (Texts(list).Any(t => t.StartsWith('A')), ReferenceEquals(keptRow, list.GetVisualDescendants().OfType<TextBlock>().First(t => t.Text == "B 0")));
            }
            finally
            {
                window.Close();
            }
        });

        Assert.Equal((false, true), result);
    }

    [Fact]
    public async Task A_row_brought_into_view_lands_below_the_sticky_header_not_under_it()
    {
        var gap = await OnUi(() =>
        {
            var (window, list, _) = Show(new Group("A", 6), new Group("B", 6));
            try
            {
                list.ScrollTo(HeaderHeight + (6 * RowHeight) + 60);   // in B, past two of its rows
                Settle(window);
                var scroll = list.GetVisualDescendants().OfType<ScrollViewer>().First();
                var above = list.GetVisualDescendants().OfType<TextBlock>().First(t => t.Text == "B 0");

                StickyGroupList.EnsureVisible(above);
                Settle(window);

                return above.TranslatePoint(new Point(0, 0), scroll)?.Y ?? -1;
            }
            finally
            {
                window.Close();
            }
        });

        Assert.True(gap >= HeaderHeight - 0.5, "the row's top is under the sticky header");
    }

    [Fact]
    public async Task The_sticky_header_takes_the_clicks_over_it_not_the_row_beneath()
    {
        var hit = await OnUi(() =>
        {
            var (window, list, _) = Show(new Group("A", 6), new Group("B", 6));
            try
            {
                list.ScrollTo(HeaderHeight + (6 * RowHeight) + 60);
                Settle(window);
                var stuck = list.GetVisualDescendants().OfType<ContentControl>().First(c => c.Classes.Contains("sticky-header"));

                return stuck.IsVisible && stuck.IsHitTestVisible && stuck.Bounds.Height >= HeaderHeight - 0.5;   // drawn above the rows and taking the pointer, so a click on it never reaches the row beneath
            }
            finally
            {
                window.Close();
            }
        });

        Assert.True(hit);
    }

    [Fact]
    public async Task Settled_layout_stays_settled()
    {
        var offsets = await OnUi(() =>
        {
            var (window, list, _) = Show(new Group("A", 6), new Group("B", 6));
            try
            {
                list.ScrollTo(HeaderHeight + (6 * RowHeight) + 10);
                Settle(window);
                var first = (list.StickyGroup, list.StickyPush);
                for (var i = 0; i < 5; i++)
                {
                    Settle(window);
                }

                return first == (list.StickyGroup, list.StickyPush);
            }
            finally
            {
                window.Close();
            }
        });

        Assert.True(offsets);
    }
}
