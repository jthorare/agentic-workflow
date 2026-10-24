using Aw.BugFix.Core.Model;
using Aw.Kernel;

namespace Aw.BugFix.Core.Ports;

/// <summary>judge resolution-check: is the reported problem solved by this change?</summary>
public interface IJudge
{
    /// <summary>Asks judge once about the written input file and returns the decision judge stored for it, naming the judge's input digest.</summary>
    /// <param name="question">The input, the file it was written to, and the judge's input digest of it.</param>
    /// <returns>The answer, or AW-BUGFIX-016 when judge gave no usable verdict (nothing to record; a retry isn't a re-roll).</returns>
    public Result<JudgeAnswer> ResolutionCheck(JudgeQuestion question);
}
