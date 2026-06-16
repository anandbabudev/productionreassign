using AdroitQMS.CA.Application.ProductionReassignWorkflowAction.Queries.GetProductionReassignUploadSummary;
using AdroitQMS.CA.Core.AuditAggregate.AdoNetSpecifications;
using AdroitQMS.CA.Core.UniverseAggregate.AdoNetSpecifications;
using Ardalis.Result;
using Immediate.Handlers.Shared;
using Infrastructure.Repositories.Interfaces;
using OpenTelemetry.Trace;

namespace AdroitQMS.CA.Application.ProductionReassign.GetProductionReassignUploadSummary;

[Handler]
public static partial class GetProductionReassignUploadSummaryHandler
{
    private static async ValueTask<Result<ReassignExcelUploadSummaryData>> HandleAsync(
        GetProductionReassignUploadSummaryQuery request,
        IAdroitQMSReadRepository adroitQmsReadRepository,
        IQMSClientDbAdoNetRepositoryFactory qmsClientDbAdoNetRepositoryFactory,
        GetProductionReassignUploadSummaryValidator validator,
        Tracer trace,
        CancellationToken token)
    {
        using var span = trace.StartActiveSpan(nameof(GetProductionReassignUploadSummaryHandler));

        var validationResult = validator.Validate(request);
        if (!validationResult.IsValid)
        {
            return Result.Error(string.Join(", ", validationResult.Errors.Select(e => e.ErrorMessage)));
        }

        if (request.JsonUploadAccountDetails is null)
        {
            return Result.Error("No data available to upload.");
        }

        if (!ProductionReassignUploadSummaryCommon.TryParseUploadAccounts(
            request.JsonUploadAccountDetails, out var uploaded))
        {
            return Result.Error("InValid Excel Data");
        }

        if (uploaded.Count == 0)
        {
            return Result.Error("No data available to upload.");
        }

        var config = await FetchAuditFormConfigAsync(
            request.AuditFormId, request.AuditLevel, adroitQmsReadRepository, token);

        var validLogIds = await FetchValidLogIdsAsync(
                request, uploaded.Select(u => u.LogID), qmsClientDbAdoNetRepositoryFactory, token);

        var evaluated = uploaded
        .Select(row => ProductionReassignUploadSummaryCommon.EvaluateRow(
            row, request.AuditType, request.ActionFromStatus, config.CommentsRequired, validLogIds))
        .ToList();
        var result = ProductionReassignUploadSummaryCommon.BuildSummary(evaluated);
        return result;
    }

    private static async Task<AuditFormConfig> FetchAuditFormConfigAsync(
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
            return new AuditFormConfig(AuditDataSourceID: 0, CommentsRequired: false);
        }

        var commentsRequired = ProductionReassignUploadSummaryCommon.ResolveCommentsRequired(
            auditLevel, form.FLReassignComment, form.SLReassignComment, form.TLReassignComment);

        return new AuditFormConfig(form.AuditDataSourceID, commentsRequired);
    }

    private static Task<ISet<int>> FetchValidLogIdsAsync(
        GetProductionReassignUploadSummaryQuery request,
        IEnumerable<int> logIds,
        IQMSClientDbAdoNetRepositoryFactory qmsClientDbAdoNetRepositoryFactory,
        CancellationToken cancellationToken) =>
        ProductionReassignUploadSummaryCommon.FetchValidLogIdsAsync(
            qmsClientDbAdoNetRepositoryFactory,
            request.ProjectGroupId,
            logIds,
            request.AuditType,
            request.AuditFormId,
            request.AuditLevel,
            request.ActionFromStatus,
            cancellationToken);
}
