using AdroitQMS.CA.Application.ProductionReassign.GetProductionReassignUploadSummary;
using AdroitQMS.CA.Application.ProductionReassignWorkflowAction.Queries.GetProductionReassignUploadSummary;

namespace AdroitQMS.CA.Application.ProductionReassign.UpdateProductionReassignUploadedData;

/// <summary>
/// Pure (data-access free) logic for <c>spNGQMS_UpdateProductionReassignUploadedData</c>:
/// the pre-apply validation gate and the status-id mappings. Parsing and per-row evaluation are
/// reused from <see cref="ProductionReassignUploadSummaryCommon"/> so both handlers agree on what
/// "SUCCESS" means.
/// </summary>
internal static class ProductionReassignUpdateCommon
{
    public const string UpdatedSuccessfully = "Updated Successfully";
    public const string NoDataToUpload = "No data available to upload.";
    public const string InvalidExcelData = "InValid Excel Data";

    // Pre-apply gate messages (the SP's -1 / Errorstatus branches).
    public const string CommentsBlank = "Comments should not blank for selected account.";
    public const string InvalidInitial = "In ReassignComments initial three letter should be in alphanumeric.";
    public const string InvalidReassignStatus = "Uploaded data has invalid ReassignStatus.";

    private static readonly HashSet<string> ValidReassignStatuses =
        new(StringComparer.OrdinalIgnoreCase) { "UnAssign", "UnHold", "Hold" };

    /// <summary>
    /// Final safety gate run over the would-be-applied (SUCCESS) rows before touching production tables —
    /// the SP's <c>@CommentEmptyCount</c> / <c>@InvalidCommentsCount</c> / <c>@InvalidStatusCount</c> /
    /// <c>@InvalidStatusIDCount</c> checks, in the same precedence order. Returns the first failing
    /// message, or <see langword="null"/> when the rows are safe to apply.
    /// </summary>
    public static string? ValidateBeforeApply(IReadOnlyList<EvaluatedRow> successRows, bool commentsRequired)
    {
        if (commentsRequired && successRows.Any(r => string.IsNullOrEmpty(r.ReassignComments)))
        {
            return CommentsBlank;
        }

        if (successRows.Any(r => ProductionReassignUploadSummaryCommon.HasNonAlphaNumericInitial(r.ReassignComments)))
        {
            return InvalidInitial;
        }

        if (successRows.Any(r => !ValidReassignStatuses.Contains(r.ReassignStatus ?? string.Empty)))
        {
            return InvalidReassignStatus;
        }

        if (successRows.Any(r => r.ToAuditStatusID == 0))
        {
            return InvalidReassignStatus;
        }

        return null;
    }

    /// <summary>
    /// New <c>tblSystemFetchLogs.ActionStatusID</c> for an applied row (AuditType 2) — the SP's
    /// <c>IIF(@pFromActionStatusID=5 AND ToAuditStatusID=6, 6, IIF(... ToAuditStatusID=8, 10, 9))</c>.
    /// </summary>
    public static int ResolveSystemFetchActionStatusId(int actionFromStatus, int toAuditStatusId) =>
        (actionFromStatus, toAuditStatusId) switch
        {
            (5, 6) => 6,
            (5, 8) => 10,
            _ => 9,
        };
}
