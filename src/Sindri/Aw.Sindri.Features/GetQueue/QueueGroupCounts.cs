namespace Aw.Sindri.Features.GetQueue;

/// <summary>The open items in each queue group, whatever page the answer shows (#2788).</summary>
/// <param name="WaitingOnYou">Items waiting on the human.</param>
/// <param name="WithYou">Items taken over.</param>
/// <param name="Running">Items running.</param>
/// <param name="Queued">Items queued.</param>
public sealed record QueueGroupCounts(int WaitingOnYou, int WithYou, int Running, int Queued);
