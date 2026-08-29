using FluentValidation;

namespace Identity.Application.TenantModules;

public sealed class UpdateTenantModuleCommandValidator : AbstractValidator<UpdateTenantModuleCommand>
{
    public UpdateTenantModuleCommandValidator()
    {
        RuleFor(x => x.TenantId)
            .NotEmpty().WithMessage("O identificador do tenant é obrigatório.");

        RuleFor(x => x.ModuleId)
            .NotEmpty().WithMessage("O identificador do módulo é obrigatório.");

        RuleFor(x => x.PlanId)
            .NotEmpty().WithMessage("O identificador do plano é obrigatório.");
    }
}
