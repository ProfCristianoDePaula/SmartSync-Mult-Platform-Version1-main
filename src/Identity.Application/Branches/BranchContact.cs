namespace Identity.Application.Branches;

/// <summary>Contato de filial (telefone + e-mail). Mesma validação do value
/// object Contact do Domain (defensiva, no serviço).</summary>
public sealed record BranchContact(
    string Phone,
    string? SecondaryPhone,
    string Email);
