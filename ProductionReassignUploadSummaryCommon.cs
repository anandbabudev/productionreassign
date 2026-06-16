using System.Text.Json;
using AdroitQMS.CA.Application.ProductionReassign.GetProductionReassignUploadSummary;
using AdroitQMS.CA.Core.AuditAggregate.AdoNetSpecifications;
using AdroitQMS.CA.Core.UniverseAggregate.AdoNetSpecifications;
using Infrastructure.Repositories.Interfaces;

namespace AdroitQMS.CA.Application.ProductionReassignWorkflowAction.Queries.GetProductionReassignUploadSummary;

internal static class ProductionReassignUploadSummaryCommon
{
    public const string Success = "SUCCESS";
    public const string InvalidReassignStatus = "Invalid ReassignStatus.";
    public const string CommentsEmpty = "ReassignComments Should Not Empty.";
    public const string InvalidInitial = "In ReassignComments Initial Three Letter Should be in AlphaNumeric.";
    public const string InvalidLogID = "Invalid LogID.";

    public const int UploadTemplateDataSourceID = 2;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
    };

    public static bool TryParseUploadAccounts(string json, out IReadOnlyList<UploadAccountRow> rows)
    {
        rows = [];
        try
        {
            var parsed = JsonSerializer.Deserialize<List<UploadAccountInput>>(json, JsonOptions) ?? [];
            rows = parsed
                .Select(p => new UploadAccountRow(
                    LogID: p.LogID,
                    ReassignComments: p.ReassignComments?.Trim(),
                    ReassignStatus: p.ReassignStatus?.Trim()))
                .ToList();
            return true;
        }
        catch (JsonException)
        {
            return false;
        }
    }

    /// <summary>
    /// Resolves the subset of uploaded LogIDs that are valid fetch/touch-log targets for the
    /// given audit form and level — the C# equivalent of the SP's "Invalid LogID." LEFT JOIN checks
    /// against tblAuditTouchLogs (AuditType 1) or tblSystemFetchLogs (AuditType 2).
    /// </summary>
    public static async Task<ISet<int>> FetchValidLogIdsAsync(
        IQMSClientDbAdoNetRepositoryFactory qmsClientDbAdoNetRepositoryFactory,
        int projectGroupId,
        IEnumerable<int> logIds,
        int auditType,
        int auditFormId,
        int auditLevel,
        int actionFromStatus,
        CancellationToken cancellationToken)
    {
        var ids = logIds.Distinct().ToList();
        if (ids.Count == 0)
        {
            return new HashSet<int>();
        }

        using var repo = await qmsClientDbAdoNetRepositoryFactory.CreateReadAsync(projectGroupId, cancellationToken);
        if (auditType == 1)
        {
            var rows = await repo.ListAsync(
                new GetValidAuditTouchLogIdsSpec(ids, auditFormId, auditLevel), cancellationToken);
            return rows.Select(r => r.AuditTouchLogID).ToHashSet();
        }

        var fetchRows = await repo.ListAsync(
            new GetValidSystemFetchLogIdsSpec(ids, auditFormId, auditLevel, actionFromStatus),
            cancellationToken);
        return fetchRows.Select(r => r.SystemFetchLogID).ToHashSet();
    }

    public static bool ResolveCommentsRequired(
        int auditLevel,
        bool? flReassignComment,
        bool? slReassignComment,
        bool? tlReassignComment) => auditLevel switch
        {
            1 => flReassignComment ?? false,
            2 => slReassignComment ?? false,
            _ => tlReassignComment ?? false,
        };

    public static EvaluatedRow EvaluateRow(
        UploadAccountRow row,
        int auditType,
        int actionFromStatus,
        bool commentsRequired,
        ISet<int> validLogIds)
    {
        var (status, toAuditStatusID) = EvaluateReassignStatus(auditType, actionFromStatus, row.ReassignStatus);

        if (status == Success && commentsRequired && string.IsNullOrEmpty(row.ReassignComments))
        {
            status = CommentsEmpty;
        }

        if (status == Success && HasNonAlphaNumericInitial(row.ReassignComments))
        {
            status = InvalidInitial;
        }

        if (status == Success && !validLogIds.Contains(row.LogID))
        {
            status = InvalidLogID;
        }

        return new EvaluatedRow(
            LogID: row.LogID,
            ReassignComments: row.ReassignComments,
            ReassignStatus: row.ReassignStatus,
            UploadedStatus: status,
            ToAuditStatusID: status == Success ? toAuditStatusID : 0);
    }

    public static (string Status, int ToAuditStatusID) EvaluateReassignStatus(
        int auditType, int actionFromStatus, string? auditStatus)
    {
        bool Is(string value) => string.Equals(auditStatus, value, StringComparison.OrdinalIgnoreCase);

        if (auditType == 1 && actionFromStatus == 5 && Is("UnAssign"))
        {
            return (Success, 8);
        }

        if (auditType == 2 && actionFromStatus == 6 && Is("UnHold"))
        {
            return (Success, 8);
        }

        if (auditType == 2 && actionFromStatus == 5 && Is("Hold"))
        {
            return (Success, 6);
        }

        if (auditType == 2 && actionFromStatus == 5 && Is("UnAssign"))
        {
            return (Success, 8);
        }

        return (InvalidReassignStatus, 0);
    }

    public static bool HasNonAlphaNumericInitial(string? comments)
    {
        var text = comments ?? string.Empty;
        var length = Math.Min(3, text.Length);
        for (var i = 0; i < length; i++)
        {
            var c = text[i];
            var isAlphaNumeric = c is (>= 'A' and <= 'Z') or (>= 'a' and <= 'z') or (>= '0' and <= '9');
            if (!isAlphaNumeric)
            {
                return true;
            }
        }

        return false;
    }

    public static ReassignExcelUploadSummaryData BuildSummary(IReadOnlyList<EvaluatedRow> evaluated)
    {
        var uploadedCount = evaluated.Count;
        var invalidCount = evaluated.Count(r => !r.IsSuccess);
        var validCount = uploadedCount == invalidCount ? 0 : uploadedCount - invalidCount;

        return new ReassignExcelUploadSummaryData
        {
            UploadedAccountCount = uploadedCount,
            InvalidAccountCount = invalidCount,
            ValidAccountCount = validCount,
            InvalidData = evaluated
                .Where(r => !r.IsSuccess)
                .Select(r => new InvalidExcelData
                {
                    LogID = r.LogID,
                    ReassignComments = r.ReassignComments,
                    ReassignStatus = r.ReassignStatus,
                    Result = r.UploadedStatus,
                })
                .ToList(),
        };
    }
}
