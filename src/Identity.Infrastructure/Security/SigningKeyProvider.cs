using System.Security.Cryptography;
using Identity.Application.Auth;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;

namespace Identity.Infrastructure.Security;

/// <summary>
/// Armazena/gera a chave RSA de assinatura em PEM (fora do repositório) e
/// expõe a chave pública para validação (JWKS) sem servidor OIDC completo.
/// </summary>
public sealed class SigningKeyProvider : IDisposable
{
    private readonly RSA _rsa;
    private readonly SigningCredentials _signingCredentials;
    private readonly JsonWebTokenHandler _tokenHandler = new();

    public SigningKeyProvider(JwtOptions options)
    {
        var keyPath = Path.GetFullPath(options.SigningKeyPath);
        var directory = Path.GetDirectoryName(keyPath);
        if (!string.IsNullOrEmpty(directory))
            Directory.CreateDirectory(directory);

        _rsa = File.Exists(keyPath) ? LoadExisting(keyPath) : CreateAndPersist(keyPath);

        var securityKey = new RsaSecurityKey(_rsa) { KeyId = options.KeyId };
        _signingCredentials = new SigningCredentials(securityKey, SecurityAlgorithms.RsaSha256);

        var rsaParams = _rsa.ExportParameters(false);
        PublicKey = new JsonWebKey
        {
            Kty = "RSA",
            Use = "sig",
            Alg = SecurityAlgorithms.RsaSha256,
            Kid = options.KeyId,
            N = Base64UrlEncoder.Encode(rsaParams.Modulus!),
            E = Base64UrlEncoder.Encode(rsaParams.Exponent!)
        };
    }

    public SigningCredentials SigningCredentials => _signingCredentials;

    public JsonWebKey PublicKey { get; }

    public JsonWebToken? ReadToken(string token) => _tokenHandler.ReadJsonWebToken(token);

    private static RSA LoadExisting(string path)
    {
        var rsa = RSA.Create();
        rsa.ImportFromPem(File.ReadAllText(path));
        return rsa;
    }

    private static RSA CreateAndPersist(string path)
    {
        var rsa = RSA.Create(2048);
        File.WriteAllText(path, rsa.ExportPkcs8PrivateKeyPem(), System.Text.Encoding.UTF8);
        return rsa;
    }

    public void Dispose() => _rsa.Dispose();
}