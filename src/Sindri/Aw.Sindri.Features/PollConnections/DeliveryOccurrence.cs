namespace Aw.Sindri.Features.PollConnections;

/// <summary>The run occurrence of a polled event: a number made from its delivery id (100,000,000 to 999,999,998), so the same event always has the same run id.</summary>
internal static class DeliveryOccurrence
{
    /// <summary>Turns a delivery id into its occurrence number.</summary>
    /// <param name="deliveryKey">The event's delivery id ('github:1001').</param>
    /// <returns>A number from 100,000,000 to 999,999,998.</returns>
    internal static int Of(string deliveryKey) => throw new NotSupportedException("unbuilt: " + deliveryKey);
}
