namespace Identity.Application.Notifications;

/// <summary>
/// Envio de SMS transacional. Implementado na Infrastructure com um provedor
/// provisto (ex.: Twilio trial). LIMITAÇÃO documentada: SMS transacional
/// ilimitado e gratuito em produção não existe — trials têm restrições
/// (somente números verificados na conta, assinatura do provedor no texto).
/// Para dev, pode-se usar a implementação "só log" (simula a entrega).
/// </summary>
public interface ISmsSender
{
    Task SendAsync(string to, string message, CancellationToken ct = default);
}