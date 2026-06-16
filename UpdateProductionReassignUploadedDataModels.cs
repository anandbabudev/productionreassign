namespace AdroitQMS.CA.Application.ProductionReassign.UpdateProductionReassignUploadedData;

/// <summary>
/// Input for <c>spNGQMS_UpdateProductionReassignUploadedData</c>.
/// <para>
/// Unlike the stored procedure — which read the rows previously persisted into
/// <c>tblStagingProductionReassignUploadAccounts</c> by the summary step — this command is
/// stateless and re-receives the same uploaded payload. The rows are re-parsed and re-evaluated
/// with the shared <c>ProductionReassignUploadSummaryCommon</c> logic so the two handlers stay
/// consistent without relying on a shared staging table.
/// </para>
/// </summary>
public sealed record UpdateProductionReassignUploadedDataCommand(
    int ProjectGroupId,
    int AuditFormId,
    int AuditType,
    int ActionFromStatus,
    int AuditLevel,
    int EmployeeId,
    string NGDatabaseName,
    string QMSDatabaseName,
    DateTime FromDate,
    DateTime ToDate,
    string? JsonUploadAccountDetails);

/// <summary>Result of a successful reassignment update (the SP's <c>Errorcode 0 / "Updated Successfully"</c>).</summary>
public sealed class UpdateProductionReassignUploadedDataResponse
{
    public int UpdatedAccountCount { get; init; }

    public string Message { get; init; } = ProductionReassignUpdateCommon.UpdatedSuccessfully;
}

/// <summary>
/// Resolved <c>NexgenQMS.dbo.tblPGAuditForms</c> row (active form, <c>AuditFormStatus = 'P'</c>):
/// the SP's <c>@AuditDataSourceID</c> / <c>@UploadTemplateID</c> / <c>@IsReassignCommentRequired</c>.
/// </summary>
internal sealed record AuditFormUpdateConfig(int AuditDataSourceID, int UploadTemplateID, bool CommentsRequired);

/// <summary>
/// Resolved <c>NexgenQMS.dbo.tblPGUploadTemplates</c> row (only loaded when
/// <see cref="AuditFormUpdateConfig.AuditDataSourceID"/> = 2): the SP's <c>@MapNG</c> / <c>@UploadTypeID</c>.
/// </summary>
internal sealed record UploadTemplateConfig(bool MapNG, int UploadTypeID);

/// <summary>
/// Everything the write side needs to apply the reassignment, beyond the per-row data. Mirrors the
/// scalar parameters the SP carried into its dynamic <c>sp_executesql</c> batch.
/// </summary>
public sealed record ReassignUpdateContext(
    int AuditFormId,
    int AuditLevel,
    int AuditType,
    int ActionFromStatus,
    int EmployeeId,
    int AuditDataSourceId,
    bool MapNg,
    int UploadTypeId,
    string NgDatabaseName,
    string QmsDatabaseName,
    DateTime FromDate,
    DateTime ToDate);

/// <summary>A single validated row to apply (the SP's staging rows with <c>UploadedStatus = 'SUCCESS'</c>).</summary>
public sealed record SuccessUploadRow(int LogId, int ToAuditStatusId, string? ActionComments);
