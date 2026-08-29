using FluentValidation;

namespace Identity.Application.Plans;

public sealed class CreatePlanCommandValidator : AbstractValidator<CreatePlanCommand>
{
    public CreatePlanCommandValidator()
    {
        RuleFor(x => x.ModuleId)
            .NotEmpty().WithMessage("O identificador do módulo é obrigatório.");

        RuleFor(x => x.Name)
            .NotEmpty().WithMessage("O nome do plano é obrigatório.")
            .MaximumLength(100).WithMessage("O nome do plano deve ter no máximo 100 caracteres.");

        RuleFor(x => x.Description)
            .MaximumLength(500).WithMessage("A descrição deve ter no máximo 500 caracteres.");

        RuleFor(x => x.MonthlyPrice)
            .GreaterThanOrEqualTo(0).WithMessage("O preço mensal não pode ser negativo.");

        RuleFor(x => x.AnnualPrice)
            .GreaterThanOrEqualTo(0).WithMessage("O preço anual não pode ser negativo.");

        RuleFor(x => x.TrialDays)
            .GreaterThanOrEqualTo(0).WithMessage("O período de teste não pode ser negativo.");

        RuleFor(x => x.Features)
            .Must(features => features is null || features.All(f => !string.IsNullOrWhiteSpace(f)))
            .WithMessage("A lista de features não pode conter itens vazios.");

        RuleFor(x => x.MaxBranches)
            .Must(limit => limit is null || limit >= 1)
            .WithMessage("O limite de filiais deve ser ao menos 1 (ou null para 'sem limite').");

        RuleFor(x => x.MaxUsers)
            .Must(limit => limit is null || limit >= 1)
            .WithMessage("O limite de usuários deve ser ao menos 1 (ou null para 'sem limite').");

        RuleFor(x => x.MaxStorageMb)
            .Must(limit => limit is null || limit >= 1)
            .WithMessage("O limite de armazenamento deve ser ao menos 1 (ou null para 'sem limite').");
    }
}
