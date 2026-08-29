using FluentValidation;
using Identity.Domain.Enums;

namespace Identity.Application.Tenants;

public sealed class UpdateTenantCommandValidator : AbstractValidator<UpdateTenantCommand>
{
    public UpdateTenantCommandValidator()
    {
        RuleFor(x => x.Id)
            .NotEmpty().WithMessage("O identificador do tenant é obrigatório.");

        RuleFor(x => x.LegalName)
            .NotEmpty().WithMessage("A razão social é obrigatória.")
            .MaximumLength(255).WithMessage("A razão social deve ter no máximo 255 caracteres.");

        RuleFor(x => x.TradeName)
            .NotEmpty().WithMessage("O nome fantasia é obrigatório.")
            .MaximumLength(255).WithMessage("O nome fantasia deve ter no máximo 255 caracteres.");

        RuleFor(x => x.Email)
            .NotEmpty().WithMessage("O e-mail é obrigatório.")
            .MaximumLength(254).WithMessage("O e-mail deve ter no máximo 254 caracteres.")
            .Matches(@"^[^@\s]+@[^@\s]+\.[^@\s]+$").WithMessage("E-mail inválido.");

        RuleFor(x => x.Status)
            .IsInEnum().WithMessage("Status inválido.");
    }
}
