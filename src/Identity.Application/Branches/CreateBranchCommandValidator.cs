using FluentValidation;

namespace Identity.Application.Branches;

public sealed class CreateBranchCommandValidator : AbstractValidator<CreateBranchCommand>
{
    public CreateBranchCommandValidator()
    {
        RuleFor(x => x.Name)
            .NotEmpty().WithMessage("O nome da filial é obrigatório.")
            .MaximumLength(255).WithMessage("O nome da filial deve ter no máximo 255 caracteres.");

        // Endereço — espelha as regras do value object Address (validação
        // defensiva; o serviço converte ArgumentException em 400).
        RuleFor(x => x.Address.Street)
            .NotEmpty().WithMessage("O logradouro é obrigatório.")
            .MaximumLength(255).WithMessage("O logradouro deve ter no máximo 255 caracteres.");

        RuleFor(x => x.Address.Number)
            .NotEmpty().WithMessage("O número é obrigatório.")
            .MaximumLength(20).WithMessage("O número deve ter no máximo 20 caracteres.");

        RuleFor(x => x.Address.Complement)
            .MaximumLength(100).WithMessage("O complemento deve ter no máximo 100 caracteres.");

        RuleFor(x => x.Address.District)
            .NotEmpty().WithMessage("O bairro é obrigatório.")
            .MaximumLength(100).WithMessage("O bairro deve ter no máximo 100 caracteres.");

        RuleFor(x => x.Address.City)
            .NotEmpty().WithMessage("A cidade é obrigatória.")
            .MaximumLength(100).WithMessage("A cidade deve ter no máximo 100 caracteres.");

        RuleFor(x => x.Address.State)
            .NotEmpty().WithMessage("O estado (UF) é obrigatório.")
            .Length(2).WithMessage("O estado deve conter 2 letras (UF).");

        RuleFor(x => x.Address.PostalCode)
            .NotEmpty().WithMessage("O CEP é obrigatório.")
            .Must(c => c is not null && c.Count(char.IsDigit) == 8)
            .WithMessage("CEP inválido: deve conter 8 dígitos.");

        // Contato — espelha as regras do value object Contact.
        RuleFor(x => x.Contact.Phone)
            .NotEmpty().WithMessage("O telefone é obrigatório.")
            .Must(p => p is not null && p.Count(char.IsDigit) is >= 10 and <= 11)
            .WithMessage("Telefone inválido: deve conter 10 (fixo) ou 11 (celular) dígitos.");

        RuleFor(x => x.Contact.SecondaryPhone)
            .Must(p => p is null || p.Count(char.IsDigit) is >= 10 and <= 11)
            .WithMessage("Telefone secundário inválido: deve conter 10 ou 11 dígitos.");

        RuleFor(x => x.Contact.Email)
            .NotEmpty().WithMessage("O e-mail de contato é obrigatório.")
            .MaximumLength(254).WithMessage("O e-mail de contato deve ter no máximo 254 caracteres.")
            .Matches(@"^[^@\s]+@[^@\s]+\.[^@\s]+$").WithMessage("E-mail de contato inválido.");
    }
}
