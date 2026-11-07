namespace Aw.Sindri.Core.Reviews;

/// <summary>What the comment loop does when it marks a 'sindri.fix:' thread fixed: closes the fix item as done and takes its task out of the fix-task files.</summary>
public interface IFixItemCloser
{
    /// <summary>Closes the item named by a fix marker. An item that is unknown or already closed is not an error: the answer says so and the loop goes on.</summary>
    /// <param name="item">The item id text from the marker.</param>
    /// <param name="cancellationToken">Cancels the call.</param>
    /// <returns>True when this call closed the item, false when it was unknown or already closed.</returns>
    public ValueTask<bool> Close(string item, CancellationToken cancellationToken);
}
