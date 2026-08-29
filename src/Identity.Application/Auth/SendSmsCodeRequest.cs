namespace Identity.Application.Auth;

/// <summary>
/// Solicitação de código SMS para validar o celular de um Client AUTENTICADO.
/// O código é o token nativo do Identity para troca de telefone
/// (GenerateChangePhoneNumberTokenAsync + VerifyChangePhoneNumberTokenAsync).
/// </summary>
public sealed record SendSmsCodeRequest(string PhoneNumber);