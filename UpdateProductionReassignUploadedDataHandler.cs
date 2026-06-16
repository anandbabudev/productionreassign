using AdroitQMS.CA.Application.ProductionReassign.GetProductionReassignUploadSummary;
using AdroitQMS.CA.Application.ProductionReassignWorkflowAction.Queries.GetProductionReassignUploadSummary;
using AdroitQMS.CA.Core.AuditAggregate.AdoNetSpecifications;
using Ardalis.Result;
using Immediate.Handlers.Shared;
using Infrastructure.Repositories.Interfaces;
using OpenTelemetry.Trace;

namespace AdroitQMS.CA.Application.ProductionReassign.UpdateProductionReassignUploadedData;

/// <summary>
/// C# translation of <c>spNGQMS_UpdateProductionReassignUploadedData</c>.
/// <para>
/// Flow: validate the request, re-parse and re-evaluate the uploaded rows (shared with the summary
/// handler), run the pre-apply safety gate over the rows that would be applied, then hand the
/// validated rows to <see cref="IProductionReassignUploadWriter"/>, which performs the cross-database
/// updates atomically. Reference data (audit form, upload template) comes from the NexgenQMS read
/// repository; log-id validity from the project's client database.
/// </para>
/// </summary>
[Handler]
public static partial class UpdateProductionReassignUploadedDataHandler
{
    private static async ValueTask<Result<UpdateProductionReassignUploadedDataResponse>> HandleAsync(
        UpdateProductionReassignUploadedDataCommand request,
        IAdroitQMSReadRepository adroitQmsReadRepository,
        IQMSClientDbAdoNetRepositoryFactory qmsClientDbAdoNetRepositoryFactory,
        IProductionReassignUploadWriter uploadWriter,
        UpdateProductionReassignUploadedDataValidator validator,
        Tracer trace,
        CancellationToken token)
    {
        using var span = trace.StartActiveSpan(nameof(UpdateProductionReassignUploadedDataHandler));

        var validationResult = validator.Validate(request);
        if (!validationResult.IsValid)
        {
            return Result.Error(string.Join(", ", validationResult.Errors.Select(e => e.ErrorMessage)));
        }

        if (request.JsonUploadAccountDetails is null)
        {
            return Result.Error(ProductionReassignUpdateCommon.NoDataToUpload);
        }

        if (!ProductionReassignUploadSummaryCommon.TryParseUploadAccounts(
            request.JsonUploadAccountDetails, out var uploaded))
        {
            return Result.Error(ProductionReassignUpdateCommon.InvalidExcelData);
        }

        if (uploaded.Count == 0)
        {
            return Result.Error(ProductionReassignUpdateCommon.NoDataToUpload);
        }

        var config = await FetchAuditFormConfigAsync(
            request.AuditFormId, request.AuditLevel, adroitQmsReadRepository, token);

        var template = config.AuditDataSourceID == ProductionReassignUploadSummaryCommon.UploadTemplateDataSourceID
            ? await FetchUploadTemplateAsync(config.UploadTemplateID, adroitQmsReadRepository, token)
            : null;

        var validLogIds = await ProductionReassignUploadSummaryCommon.FetchValidLogIdsAsync(
            qmsClientDbAdoNetRepositoryFactory,
            request.ProjectGroupId,
            uploaded.Select(u => u.LogID),
            request.AuditType,
            request.AuditFormId,
            request.AuditLevel,
            request.ActionFromStatus,
            token);

        var successRows = uploaded
            .Select(row => ProductionReassignUploadSummaryCommon.EvaluateRow(
                row, request.AuditType, request.ActionFromStatus, config.CommentsRequired, validLogIds))
            .Where(r => r.IsSuccess)
            .ToList();

        var gateMessage = ProductionReassignUpdateCommon.ValidateBeforeApply(successRows, config.CommentsRequired);
        if (gateMessage is not null)
        {
            return Result.Error(gateMessage);
        }

        var context = new ReassignUpdateContext(
            AuditFormId: request.AuditFormId,
            AuditLevel: request.AuditLevel,
            AuditType: request.AuditType,
            ActionFromStatus: request.ActionFromStatus,
            EmployeeId: request.EmployeeId,
            AuditDataSourceId: config.AuditDataSourceID,
            MapNg: template?.MapNG ?? false,
            UploadTypeId: template?.UploadTypeID ?? 0,
            NgDatabaseName: request.NGDatabaseName,
            QmsDatabaseName: request.QMSDatabaseName,
            FromDate: request.FromDate,
            ToDate: request.ToDate);

        var rowsToApply = successRows
            .Select(r => new SuccessUploadRow(r.LogID, r.ToAuditStatusID, r.ReassignComments))
            .ToList();

        var updatedCount = await uploadWriter.ApplyReassignmentsAsync(
            request.ProjectGroupId, context, rowsToApply, token);

        return Result.Success(new UpdateProductionReassignUploadedDataResponse
        {
            UpdatedAccountCount = updatedCount,
            Message = ProductionReassignUpdateCommon.UpdatedSuccessfully,
        });
    }

    private static async Task<AuditFormUpdateConfig> FetchAuditFormConfigAsync(
        int auditFormID,
        int auditLevel,
        IAdroitQMSReadRepository adroitQmsReadRepository,
        CancellationToken cancellationToken)
    {
        var rows = await adroitQmsReadRepository.ListAsync(
            new GetAuditFormInfoByIDSpec(auditFormID), cancellationToken);
        var form = rows.FirstOrDefault();
        if (form is null)
        {
            // No active ('P') form — the SP leaves @AuditDataSourceID = 0 and still completes,
            // so the writer simply skips the audit-status update.
            return new AuditFormUpdateConfig(AuditDataSourceID: 0, UploadTemplateID: 0, CommentsRequired: false);
        }

        var commentsRequired = ProductionReassignUploadSummaryCommon.ResolveCommentsRequired(
            auditLevel, form.FLReassignComment, form.SLReassignComment, form.TLReassignComment);

        return new AuditFormUpdateConfig(form.AuditDataSourceID, form.UploadTemplateID, commentsRequired);
    }

    private static async Task<UploadTemplateConfig?> FetchUploadTemplateAsync(
        int uploadTemplateID,
        IAdroitQMSReadRepository adroitQmsReadRepository,
        CancellationToken cancellationToken)
    {
        if (uploadTemplateID <= 0)
        {
            return null;
        }

        var rows = await adroitQmsReadRepository.ListAsync(
            new GetUploadTemplateByIDSpec(uploadTemplateID), cancellationToken);
        var template = rows.FirstOrDefault();
        return template is null
            ? null
            : new UploadTemplateConfig(template.MapNGUniqueColumns, template.UploadTypeID);
    }
}
