namespace AdroitQMS.CA.Application.ProductionReassign.UpdateProductionReassignUploadedData;

/// <summary>
/// Write-side port for applying validated production-reassignment rows. The single method replaces
/// the entire transactional tail of <c>spNGQMS_UpdateProductionReassignUploadedData</c> and MUST run
/// every step atomically (one transaction; roll back and surface the error on any failure):
/// <list type="number">
///   <item>
///     <description>
///     <b>AuditType 2 only</b> — update <c>tblSystemFetchLogs</c> (<c>ActionStatusID</c> via
///     <c>ProductionReassignUpdateCommon.ResolveSystemFetchActionStatusId</c>, <c>ActionByID</c>,
///     <c>ActionedOnIST</c>) for the success rows, then snapshot them into
///     <c>tblSystemFetchLogHistories</c>.
///     </description>
///   </item>
///   <item>
///     <description>
///     Update the audit-level status column (<c>FirstLevel</c>/<c>SecondLevel</c>/<c>ThirdLevelAuditStatusID</c>,
///     chosen by <see cref="ReassignUpdateContext.AuditLevel"/>) to each row's <c>ToAuditStatusId</c> on the
///     resolved NG/QMS account tables. Which joins apply is selected by
///     <see cref="ReassignUpdateContext.AuditDataSourceId"/> (1 vs 2),
///     <see cref="ReassignUpdateContext.MapNg"/> and <see cref="ReassignUpdateContext.UploadTypeId"/>.
///     Skipped entirely when <c>AuditDataSourceId</c> is neither 1 nor 2.
///     </description>
///   </item>
///   <item>
///     <description>
///     <b>AuditType 1 only</b> — mark <c>tblAuditTouchLogs</c> rows skipped (<c>TouchedBy</c>,
///     <c>SkippedOnIST</c>, <c>ActionComments</c>) for the success rows.
///     </description>
///   </item>
/// </list>
/// The implementation lives in the infrastructure layer, which owns the cross-database (NG/QMS) SQL,
/// the IST timestamp (<c>HRMS.dbo.FNHRMS_CONVERTIST</c>) and error logging
/// (<c>spNGQMS_InsertNexgenQMSErrorLog</c>).
/// </summary>
public interface IProductionReassignUploadWriter
{
    /// <summary>Applies the rows and returns the number of accounts updated. A no-op when <paramref name="successRows"/> is empty.</summary>
    Task<int> ApplyReassignmentsAsync(
        int projectGroupId,
        ReassignUpdateContext context,
        IReadOnlyCollection<SuccessUploadRow> successRows,
        CancellationToken cancellationToken);
}
