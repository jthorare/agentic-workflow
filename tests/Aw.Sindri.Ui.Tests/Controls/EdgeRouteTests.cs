using Avalonia;
using Aw.Kernel.Testing;
using Aw.Sindri.Ui.Views.Controls;

namespace Aw.Sindri.Ui.Tests.Controls;

/// <summary>The geometry of the context map's edges from fixed coordinates (F3 spec 1 §2): elbows with 12 px arcs inside a band, S-curves between bands.</summary>
[Lane(TestLane.Unit)]
public sealed class EdgeRouteTests
{
    [Fact]
    public void A_curve_leaves_and_arrives_vertically_with_handles_half_the_climb_long()
    {
        var route = EdgeRoute.Curve(new Point(100, 200), new Point(300, 600));

        var step = Assert.Single(route.Steps);
        Assert.Equal(RouteStepKind.Cubic, step.Kind);
        Assert.Equal(new Point(100, 200), route.Start);
        Assert.Equal(new Point(300, 600), route.End);
        Assert.Equal(new Point(100, 400), step.Control1);
        Assert.Equal(new Point(300, 400), step.Control2);
    }

    [Fact]
    public void A_short_climb_still_leaves_the_node_with_a_40_px_handle()
    {
        var step = Assert.Single(EdgeRoute.Curve(new Point(0, 0), new Point(50, 20)).Steps);

        Assert.Equal(new Point(0, 40), step.Control1);
        Assert.Equal(new Point(50, -20), step.Control2);
    }

    [Fact]
    public void A_curve_going_up_has_its_handles_pointing_up_from_the_start()
    {
        var step = Assert.Single(EdgeRoute.Curve(new Point(100, 700), new Point(120, 400)).Steps);

        Assert.Equal(new Point(100, 550), step.Control1);
        Assert.Equal(new Point(120, 550), step.Control2);
    }

    [Fact]
    public void An_elbow_rounds_each_corner_with_an_arc_of_12_px()
    {
        var route = EdgeRoute.Bus(new Point(100, 100), new Point(300, 200), 150);

        Assert.Equal(new Point(100, 100), route.Start);
        Assert.Equal(
            [
                new RouteStep(RouteStepKind.Line, new Point(100, 138)),
                new RouteStep(RouteStepKind.Arc, new Point(112, 150), 12, Clockwise: false),
                new RouteStep(RouteStepKind.Line, new Point(288, 150)),
                new RouteStep(RouteStepKind.Arc, new Point(300, 162), 12, Clockwise: true),
                new RouteStep(RouteStepKind.Line, new Point(300, 200)),
            ],
            route.Steps);
    }

    [Fact]
    public void An_elbow_turning_the_other_way_sweeps_the_other_way()
    {
        var route = EdgeRoute.Elbow([new Point(0, 0), new Point(100, 0), new Point(100, 100)]);

        Assert.Equal(new RouteStep(RouteStepKind.Arc, new Point(100, 12), 12, Clockwise: true), route.Steps[1]);
    }

    [Fact]
    public void A_corner_on_a_short_side_gets_a_smaller_radius()
    {
        var route = EdgeRoute.Elbow([new Point(0, 0), new Point(0, 10), new Point(100, 10)]);

        Assert.Equal(new RouteStep(RouteStepKind.Line, new Point(0, 5)), route.Steps[0]);
        Assert.Equal(new RouteStep(RouteStepKind.Arc, new Point(5, 10), 5, Clockwise: false), route.Steps[1]);
    }

    [Fact]
    public void Points_in_a_straight_line_get_no_arc()
    {
        var route = EdgeRoute.Elbow([new Point(0, 0), new Point(50, 0), new Point(100, 0)]);

        Assert.All(route.Steps, s => Assert.Equal(RouteStepKind.Line, s.Kind));
        Assert.Equal(new Point(100, 0), route.End);
    }

    [Fact]
    public void A_straight_edge_is_one_line()
    {
        var route = EdgeRoute.Straight(new Point(10, 10), new Point(60, 10));

        Assert.Equal([new RouteStep(RouteStepKind.Line, new Point(60, 10))], route.Steps);
        Assert.Equal(new Point(35, 10), route.Midpoint());
    }

    [Fact]
    public void The_label_sits_at_the_curves_middle()
    {
        var mid = EdgeRoute.Curve(new Point(100, 200), new Point(300, 600)).Midpoint();

        Assert.Equal(200, mid.X, 6);
        Assert.Equal(400, mid.Y, 6);
    }

    [Fact]
    public void The_label_of_a_symmetric_elbow_sits_half_way_along_its_run()
    {
        var mid = EdgeRoute.Bus(new Point(100, 100), new Point(300, 200), 150).Midpoint();

        Assert.Equal(200, mid.X, 6);
        Assert.Equal(150, mid.Y, 6);
    }

    [Fact]
    public void The_distance_to_a_straight_edge_is_to_the_nearest_point_of_it()
    {
        var route = EdgeRoute.Straight(new Point(0, 0), new Point(100, 0));

        Assert.Equal(10, route.DistanceTo(new Point(50, 10)), 6);
        Assert.Equal(0, route.DistanceTo(new Point(30, 0)), 6);
        Assert.Equal(5, route.DistanceTo(new Point(-3, 4)), 6);
        Assert.Equal(5, route.DistanceTo(new Point(103, -4)), 6);
    }

    [Fact]
    public void The_distance_follows_a_rounded_corner_not_the_sharp_one()
    {
        var route = EdgeRoute.Elbow([new Point(100, 100), new Point(100, 150), new Point(300, 150)]);

        // the arc's middle, 12 px of radius around (112, 138), cuts the corner at (100, 150)
        Assert.True(route.DistanceTo(new Point(103.515, 146.485)) < 0.2);
        Assert.True(route.DistanceTo(new Point(100, 150)) > 3.5);
    }

    [Fact]
    public void The_distance_follows_a_curve_through_its_middle()
    {
        var route = EdgeRoute.Curve(new Point(100, 200), new Point(300, 600));

        Assert.True(route.DistanceTo(route.Midpoint()) < 0.5);
        Assert.True(route.DistanceTo(new Point(100, 200)) < 0.001);
        Assert.True(route.DistanceTo(new Point(300, 600)) < 0.001);
        Assert.True(route.DistanceTo(new Point(200, 300)) > 20);
    }
}
