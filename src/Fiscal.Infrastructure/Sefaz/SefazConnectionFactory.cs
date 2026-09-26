using System.Net.Security;
using System.Security.Cryptography.X509Certificates;
using Microsoft.Extensions.Caching.Memory;

namespace Fiscal.Infrastructure.Sefaz;

/// <summary>
/// Conexões mTLS por certificado (R8/R9 do material-base): um
/// SocketsHttpHandler por thumbprint+validade, com descarte por expiração de
/// cache (30 min). Rotação do certificado gera outro handler. NUNCA desativa
/// a validação do certificado do servidor.
/// </summary>
public interface ISefazConnectionFactory
{
    HttpClient Create(X509Certificate2 certificado);
}

public sealed class SefazConnectionFactory(IMemoryCache cache) : ISefazConnectionFactory
{
    public HttpClient Create(X509Certificate2 certificado)
    {
        var chave = $"sefaz:{certificado.Thumbprint}:{certificado.NotAfter.Ticks}";
        if (cache.TryGetValue(chave, out HttpClient? cached) && cached is not null)
            return cached;

        var handler = new SocketsHttpHandler
        {
            PooledConnectionLifetime = TimeSpan.FromMinutes(30),
            SslOptions = new SslClientAuthenticationOptions
            {
                ClientCertificates = new X509CertificateCollection { certificado }
            }
        };

        var client = new HttpClient(handler, disposeHandler: true)
        {
            Timeout = TimeSpan.FromSeconds(60)
        };

        cache.Set(chave, client, new MemoryCacheEntryOptions()
            .SetSlidingExpiration(TimeSpan.FromMinutes(30))
            .RegisterPostEvictionCallback((_, value, _, _) =>
            {
                if (value is HttpClient c) c.Dispose();
            }));

        return client;
    }
}
