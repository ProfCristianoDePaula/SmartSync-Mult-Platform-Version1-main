using FluentValidation;

namespace Identity.Application.Modules;

public sealed class UpdateModuleCommandValidator : AbstractValidator<UpdateModuleCommand>
{
    public UpdateModuleCommandValidator()
    {
        RuleFor(x => x.Id)
            .NotEmpty().WithMessage("O identificador do módulo é obrigatório.");

        RuleFor(x => x.Name)
            .NotEmpty().WithMessage("O nome do módulo é obrigatório.")
            .MaximumLength(100).WithMessage("O nome do módulo deve ter no máximo 100 caracteres.");

        RuleFor(x => x.Description)
            .MaximumLength(500).WithMessage("A descrição deve ter no máximo 500 caracteres.");
    }
}
