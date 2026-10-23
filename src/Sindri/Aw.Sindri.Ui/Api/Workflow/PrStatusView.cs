namespace Aw.Sindri.Ui.Api.Workflow;

/// <summary>The body of the fact "fact:prStatus" (engine #774): what the merge stop's pull request looks like now.</summary>
/// <param name="Pr">The pull request's number.</param>
/// <param name="Url">The pull request's link.</param>
/// <param name="Checks">The checks; null while they have not been read.</param>
/// <param name="Review">The review threads.</param>
/// <param name="Proof">The proof run; null while it has not been read.</param>
/// <param name="Docs">The documentation state.</param>
internal sealed record PrStatusView(int Pr, string Url, PrChecksView? Checks, PrReviewView Review, PrProofView? Proof, PrDocsView Docs);
