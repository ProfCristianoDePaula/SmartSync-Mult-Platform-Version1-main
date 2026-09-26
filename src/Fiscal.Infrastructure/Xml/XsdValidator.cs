using System.Xml;
using System.Xml.Schema;
using Fiscal.Application.Xml;
using Fiscal.Domain.Common;
using Microsoft.Extensions.Hosting;

namespace Fiscal.Infrastructure.Xml;

/// <summary>
/// Validador XSD (R8): DTD proibido, XmlResolver nulo, limite de tamanho,
/// schemas de pacote local confiável. Modo "oficial/4.00" exige o pacote
/// instalado (400 claro se ausente); modo "simulador" usa o XSD didático.
/// XSD não substitui cálculo fiscal nem regras do autorizador.
/// </summary>
public sealed class XsdValidator(IHostEnvironment env) : IXsdValidator
{
    private const long MaxBytes = 5_000_000;

    public void Validar(string xml, string modo)
    {
        if (string.IsNullOrWhiteSpace(xml) || xml.Length > MaxBytes)
            throw new BusinessRuleViolationException("XML vazio ou excede 5 MB.");

        var raiz = LocalizarRaizSchemas(env.ContentRootPath);
        var set = new XmlSchemaSet();
        if (string.Equals(modo, "simulador", StringComparison.OrdinalIgnoreCase))
        {
            var xsd = raiz is null
                ? null
                : Path.Combine(raiz, "schemas", "simulador", "nfe-simplificada.xsd");
            if (xsd is null || !File.Exists(xsd))
                throw new BusinessRuleViolationException("XSD didático do simulador não instalado.");
            set.Add("urn:smartsync:fiscal:simulador", xsd);
        }
        else if (string.Equals(modo, "oficial/4.00", StringComparison.OrdinalIgnoreCase))
        {
            var dir = raiz is null ? null : Path.Combine(raiz, "schemas", "nfe", "4.00");
            var arquivos = dir is not null && Directory.Exists(dir)
                ? Directory.GetFiles(dir, "*.xsd")
                : [];
            if (arquivos.Length == 0)
                throw new BusinessRuleViolationException(
                    "Pacote oficial de schemas não instalado (schemas/nfe/4.00/).");
            foreach (var arquivo in arquivos)
                set.Add(null, arquivo);
        }
        else
        {
            throw new BusinessRuleViolationException($"Modo de validação desconhecido: {modo}.");
        }

        set.Compile();

        var erros = new List<string>();
        var settings = new XmlReaderSettings
        {
            DtdProcessing = DtdProcessing.Prohibit,
            XmlResolver = null,
            ValidationType = ValidationType.Schema,
            Schemas = set,
            MaxCharactersInDocument = MaxBytes
        };
        settings.ValidationEventHandler += (_, e) => erros.Add(e.Message);

        using var texto = new StringReader(xml);
        using var reader = XmlReader.Create(texto, settings);
        while (reader.Read()) { }

        if (erros.Count > 0)
            throw new BusinessRuleViolationException("XML fora do schema: " + string.Join(" | ", erros.Take(5)));
    }

    /// <summary>
    /// Localiza a raiz do repositório (pasta com `schemas/`) subindo até
    /// 4 níveis do ContentRoot (API, testes e container).
    /// </summary>
    internal static string? LocalizarRaizSchemas(string contentRoot)
    {
        var dir = new DirectoryInfo(contentRoot);
        for (var i = 0; i < 5 && dir is not null; i++, dir = dir.Parent)
        {
            if (Directory.Exists(Path.Combine(dir.FullName, "schemas")))
                return dir.FullName;
        }
        return null;
    }
}
