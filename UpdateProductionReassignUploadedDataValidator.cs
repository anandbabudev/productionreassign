using FluentValidation;

namespace AdroitQMS.CA.Application.ProductionReassign.UpdateProductionReassignUploadedData;

public sealed class UpdateProductionReassignUploadedDataValidator
    : AbstractValidator<UpdateProductionReassignUploadedDataCommand>
{
    public UpdateProductionReassignUploadedDataValidator()
    {
        RuleFor(x => x.ProjectGroupId).GreaterThan(0).WithMessage("Invalid ProjectGroupId");
        RuleFor(x => x.AuditFormId).GreaterThan(0).WithMessage("Invalid AuditFormId");
        RuleFor(x => x.AuditType).GreaterThan(0).WithMessage("Invalid AuditType");
        RuleFor(x => x.ActionFromStatus).GreaterThan(0).WithMessage("Invalid ActionFromStatus");
        RuleFor(x => x.AuditLevel).InclusiveBetween(1, 3).WithMessage("Invalid AuditLevel");
        RuleFor(x => x.EmployeeId).GreaterThan(0).WithMessage("Invalid EmployeeId");
        RuleFor(x => x.NGDatabaseName).NotEmpty().WithMessage("Invalid NGDatabaseName");
        RuleFor(x => x.QMSDatabaseName).NotEmpty().WithMessage("Invalid QMSDatabaseName");
        RuleFor(x => x.ToDate)
            .GreaterThanOrEqualTo(x => x.FromDate)
            .WithMessage("ToDate must be on or after FromDate");
    }
}
