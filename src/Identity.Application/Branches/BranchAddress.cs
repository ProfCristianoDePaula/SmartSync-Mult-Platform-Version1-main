namespace Identity.Application.Branches;

/// <summary>Endereço de filial (contrato de entrada/saída da API). A validação
/// defensiva no value object Address do Domain continua existindo no serviço.</summary>
public sealed record BranchAddress(
    string Street,
    string Number,
    string? Complement,
    string District,
    string City,
    string State,
    string PostalCode);
