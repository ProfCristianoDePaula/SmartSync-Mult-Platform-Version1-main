namespace Fiscal.Application.Certificados;

/// <summary>
/// Cofre de segredos (AES-256-GCM hoje; interface pronta para Key Vault/KMS).
/// Cifra PFX/senhas/CSC em repouso. NUNCA hash (o worker precisa recuperar).
/// </summary>
public interface ISecretProtector
{
    string KeyIdAtual { get; }
    byte[] Protect(byte[] plain);
    byte[] Unprotect(string keyId, byte[] cipher);
}
