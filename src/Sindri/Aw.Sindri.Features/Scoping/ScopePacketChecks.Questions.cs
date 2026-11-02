using Aw.Sindri.Core.Scoping;

namespace Aw.Sindri.Features.Scoping;

internal static partial class ScopePacketChecks
{
    /// <summary>SCP-QST-001: every interview question has two to four options and exactly one of them recommended, like a design decision (DSN-DEC-001); the owner's fastest path is the recommended option, and a delegated run can only follow one.</summary>
    private static void QuestionsRecommended(ScopeContext c, RuleSink s)
    {
        if (!s.Need("questions"))
        {
            return;
        }

        foreach (var (id, message, fix) in QuestionShape.Faults(c.Facts["questions"]))
        {
            s.Fail(id, message, fix);
        }
    }
}
