using FluentValidation;
using Identity.Domain.Enums;

namespace Identity.Application.Tenants;

public sealed class CreateTenantCommandValidator : AbstractValidator<CreateTenantCommand>
{
    public CreateTenantCommandValidator()
    {
        RuleFor(x => x.LegalName)
            .NotEmpty().WithMessage("A razão social é obrigatória.")
            .MaximumLength(255).WithMessage("A razão social deve ter no máximo 255 caracteres.");

        RuleFor(x => x.TradeName)
            .NotEmpty().WithMessage("O nome fantasia é obrigatório.")
            .MaximumLength(255).WithMessage("O nome fantasia deve ter no máximo 255 caracteres.");

        RuleFor(x => x.TipoPessoa)
            .IsInEnum().WithMessage("Tipo de pessoa inválido. Use Fisica (CPF) ou Juridica (CNPJ).");

        // Documento é obrigatório sempre; a quantidade de dígitos varia com o
        // tipo de pessoa (CPF 11 / CNPJ 14). Os dígitos verificadores são
        // validados pelo value object Documento/Cpf/Cnpj do Domain (conversão de
        // ArgumentException no serviço). Mensagens diferenciadas por tipo.
        RuleFor(x => x.Documento)
            .NotEmpty().WithMessage("O documento é obrigatório.");

        When(x => x.TipoPessoa == TipoPessoa.Fisica, () =>
        {
            RuleFor(x => x.Documento)
                .Must(d => d is not null && d.Count(char.IsDigit) == 11)
                .WithMessage("CPF inválido: deve conter 11 dígitos numéricos.");
        });

        When(x => x.TipoPessoa == TipoPessoa.Juridica, () =>
        {
            RuleFor(x => x.Documento)
                .Must(d => d is not null && d.Count(char.IsDigit) == 14)
                .WithMessage("CNPJ inválido: deve conter 14 dígitos numéricos.");
        });

        // Mesmo formato do value object Email do Domain (validação defensiva no
        // serviço continua existindo: nunca dependemos apenas do validator).
        RuleFor(x => x.Email)
            .NotEmpty().WithMessage("O e-mail é obrigatório.")
            .MaximumLength(254).WithMessage("O e-mail deve ter no máximo 254 caracteres.")
            .Matches(@"^[^@\s]+@[^@\s]+\.[^@\s]+$").WithMessage("E-mail inválido.");
    }
}
