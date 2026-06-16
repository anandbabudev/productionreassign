using AdroitQMS.CA.Application.ProductionReassignWorkflowAction.Queries.GetProductionReassignUploadSummary;

namespace AdroitQMS.CA.Application.ProductionReassign.GetProductionReassignUploadSummary;

public sealed record GetProductionReassignUploadSummaryQuery(
    int ProjectGroupId,
    int UploadedBy,
    int AuditFormId,
    string? JsonUploadAccountDetails,
    int AuditLevel,
    int AuditType,
    int ActionFromStatus);

public class ReassignExcelUploadSummaryData
{
    public int UploadedAccountCount { get; set; }

    public int InvalidAccountCount { get; set; }

    public int ValidAccountCount { get; set; }

    public List<InvalidExcelData>? InvalidData { get; set; }
}

public sealed class InvalidExcelData
{
    public int LogID { get; set; }

    public string ReassignComments { get; set; }

    public string? ReassignStatus { get; set; }

    public string Result { get; set; }
}

internal sealed record UploadAccountInput(int LogID, string? ReassignComments, string? ReassignStatus);

internal sealed record UploadAccountRow(int LogID, string? ReassignComments, string? ReassignStatus);

internal sealed record EvaluatedRow(
    int LogID,
    string? ReassignComments,
    string? ReassignStatus,
    string UploadedStatus,
    int ToAuditStatusID)
{
    public bool IsSuccess => UploadedStatus == ProductionReassignUploadSummaryCommon.Success;
}

// Resolved tblPGAuditForms config (the @AuditDataSourceID / @CommentsRequired SELECT).
internal sealed record AuditFormConfig(byte AuditDataSourceID, bool CommentsRequired);
