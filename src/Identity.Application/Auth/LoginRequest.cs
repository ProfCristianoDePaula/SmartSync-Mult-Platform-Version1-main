namespace Identity.Application.Auth;

public sealed record LoginRequest(string Identifier, string Password);