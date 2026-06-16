using FluentValidation;

namespace AdroitQMS.CA.Application.ProductionReassign.GetProductionReassignUploadSummary;

public sealed class GetProductionReassignUploadSummaryValidator : AbstractValidator<GetProductionReassignUploadSummaryQuery>
{
    public GetProductionReassignUploadSummaryValidator()
    {
        RuleFor(x => x.ProjectGroupId).GreaterThan(0).WithMessage("Invalid ProjectGroupId");
        RuleFor(x => x.UploadedBy).GreaterThan(0).WithMessage("Invalid UploadedBy");
        RuleFor(x => x.AuditFormId).GreaterThan(0).WithMessage("Invalid AuditFormId");
        RuleFor(x => x.AuditLevel).InclusiveBetween(1, 3).WithMessage("Invalid AuditLevel");
        RuleFor(x => x.AuditType).GreaterThan(0).WithMessage("Invalid AuditType");
        RuleFor(x => x.ActionFromStatus).GreaterThan(0).WithMessage("Invalid ActionFromStatus");
    }
}
