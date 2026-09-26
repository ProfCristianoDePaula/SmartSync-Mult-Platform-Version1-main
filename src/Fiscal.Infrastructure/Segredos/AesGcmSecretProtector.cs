using System.Security.Cryptography;
using Fiscal.Application.Certificados;
using Fiscal.Domain.Common;
using Microsoft.Extensions.Logging;

namespace Fiscal.Infrastructure.Segredos;

/// <summary>Opções do cofre — seção "Fiscal:Secrets". Chave mestra FORA do banco (R4).</summary>
public sealed class FiscalSecretsOptions
{
    public const string SectionName = "Fiscal:Secrets";

    public string ActiveKeyId { get; set; } = "";
    public List<MasterKeyEntry> MasterKeys { get; set; } = [];

    public sealed class MasterKeyEntry
    {
        public string KeyId { get; set; } = "";
        public string KeyHex { get; set; } = "";
    }
}

/// <summary>
/// Cofre AES-256-GCM: envelope = keyId implícito (coluna) + nonce(12) +
/// ciphertext + tag(16). Sem a chave mestra o serviço NÃO sobe em Production
/// (fail-fast no DI); em Development/teste usa chave efêmera com aviso.
/// Interface pronta para Key Vault/KMS (R4).
/// </summary>
public sealed class AesGcmSecretProtector : ISecretProtector
{
    private readonly Dictionary<string, byte[]> _keys;
    public string KeyIdAtual { get; }

    public AesGcmSecretProtector(IReadOnlyDictionary<string, byte[]> keys, string activeKeyId)
    {
        _keys = new Dictionary<string, byte[]>(keys);
        KeyIdAtual = activeKeyId;
    }

    public byte[] Protect(byte[] plain)
        => ProtectWith(KeyIdAtual, plain);

    public byte[] Unprotect(string keyId, byte[] cipher)
    {
        if (!_keys.TryGetValue(keyId, out var key))
            throw new BusinessRuleViolationException($"Chave '{keyId}' indisponível para decifrar.");
        return Decrypt(key, cipher);
    }

    private byte[] ProtectWith(string keyId, byte[] plain)
    {
        var key = _keys[keyId];
        var nonce = RandomNumberGenerator.GetBytes(12);
        var cipher = new byte[plain.Length];
        var tag = new byte[16];
        using var aes = new AesGcm(key, 16);
        aes.Encrypt(nonce, plain, cipher, tag);
        return [.. nonce, .. cipher, .. tag];
    }

    private static byte[] Decrypt(byte[] key, byte[] envelope)
    {
        try
        {
            var nonce = envelope[..12];
            var tag = envelope[^16..];
            var cipher = envelope[12..^16];
            var plain = new byte[cipher.Length];
            using var aes = new AesGcm(key, 16);
            aes.Decrypt(nonce, cipher, tag, plain);
            return plain;
        }
        catch (Exception ex) when (ex is not BusinessRuleViolationException)
        {
            throw new BusinessRuleViolationException("Falha ao decifrar segredo.");
        }
    }
}

public static class SecretProtectorFactory
{
    public static ISecretProtector Build(
        FiscalSecretsOptions options, bool isProduction, ILogger logger)
    {
        var keys = new Dictionary<string, byte[]>();
        foreach (var entry in options.MasterKeys)
        {
            if (string.IsNullOrWhiteSpace(entry.KeyId) || string.IsNullOrWhiteSpace(entry.KeyHex))
                continue;
            try { keys[entry.KeyId] = Convert.FromHexString(entry.KeyHex.Trim()); }
            catch { throw new InvalidOperationException($"Chave '{entry.KeyId}' inválida (esperado hex 64)."); }
            if (keys[entry.KeyId].Length != 32)
                throw new InvalidOperationException($"Chave '{entry.KeyId}' deve ter 256 bits (64 hex).");
        }

        if (keys.Count == 0)
        {
            if (isProduction)
                throw new InvalidOperationException(
                    "FISCAL_MASTER_KEY ausente: o serviço NÃO sobe em Production sem chave mestra (R4).");
            var efemera = RandomNumberGenerator.GetBytes(32);
            logger.LogWarning("Cofre fiscal com chave EFÊMERA (dev/teste): segredos não sobrevivem a restart.");
            return new AesGcmSecretProtector(
                new Dictionary<string, byte[]> { ["efemera-dev"] = efemera }, "efemera-dev");
        }

        var active = string.IsNullOrWhiteSpace(options.ActiveKeyId)
            ? keys.Keys.First()
            : options.ActiveKeyId;
        if (!keys.ContainsKey(active))
            throw new InvalidOperationException($"ActiveKeyId '{active}' sem chave correspondente.");

        return new AesGcmSecretProtector(keys, active);
    }
}
