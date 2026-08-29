namespace Identity.Application.Auth;

/// <summary>
/// Confirmação do celular: informa o número validado e o código recebido por
/// SMS (token nativo do Identity). Ao confirmar, PhoneNumber fica persistido e
/// PhoneNumberConfirmed = true.
/// </summary>
public sealed record ConfirmPhoneRequest(string PhoneNumber, string Code);