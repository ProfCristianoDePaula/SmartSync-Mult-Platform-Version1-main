namespace Estoque.Domain.Common;

/// <summary>
/// Referência LÓGICA à filial do microsserviço Identity — Guid opaco.
/// O Estoque NÃO possui FK física para o banco alheio (bancos separados;
/// consulta direta é proibida pelo contrato do Identity §12.1).
/// </summary>
public readonly record struct BranchId(Guid Value)
{
    public static BranchId From(Guid value) => new(value);
    public override string ToString() => Value.ToString();
}
