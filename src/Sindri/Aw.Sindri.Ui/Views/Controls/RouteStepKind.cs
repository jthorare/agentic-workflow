namespace Aw.Sindri.Ui.Views.Controls;

/// <summary>The kinds of step a route is made of.</summary>
internal enum RouteStepKind
{
    /// <summary>A straight line to <see cref="RouteStep.To"/>.</summary>
    Line,

    /// <summary>An arc of <see cref="RouteStep.Radius"/> to <see cref="RouteStep.To"/>, turning <see cref="RouteStep.Clockwise"/>.</summary>
    Arc,

    /// <summary>A cubic curve to <see cref="RouteStep.To"/> through <see cref="RouteStep.Control1"/> and <see cref="RouteStep.Control2"/>.</summary>
    Cubic,
}
