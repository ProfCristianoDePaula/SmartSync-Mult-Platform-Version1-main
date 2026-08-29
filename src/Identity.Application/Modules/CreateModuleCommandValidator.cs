using FluentValidation;

namespace Identity.Application.Modules;

public sealed class CreateModuleCommandValidator : AbstractValidator<CreateModuleCommand>
{
    public CreateModuleCommandValidator()
    {
        RuleFor(x => x.Name)
            .NotEmpty().WithMessage("O nome do módulo é obrigatório.")
            .MaximumLength(100).WithMessage("O nome do módulo deve ter no máximo 100 caracteres.");

        RuleFor(x => x.Slug)
            .NotEmpty().WithMessage("O slug do módulo é obrigatório.")
            .MaximumLength(100).WithMessage("O slug do módulo deve ter no máximo 100 caracteres.")
            .Matches(@"^[a-z0-9]+(-[a-z0-9]+)*$")
            .WithMessage("Slug inválido: use apenas letras minúsculas, números e hífens (ex.: \"agro\", \"ecommerce\", \"agenda\").");

        RuleFor(x => x.Description)
            .MaximumLength(500).WithMessage("A descrição deve ter no máximo 500 caracteres.");
    }
}
