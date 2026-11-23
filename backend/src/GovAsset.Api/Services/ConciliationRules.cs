using GovAsset.Api.Domain;

namespace GovAsset.Api.Services;

public record DivergenciaProposta(int RegistroOficialId, Severidade Severidade, string Descricao);

/// <summary>
/// Regras puras (sem banco) do motor de conciliação. Comparam, por escola e categoria, um relato com os
/// registros oficiais. O resultado é sempre um INDICATIVO para análise da gestão, nunca uma conclusão.
/// </summary>
public static class ConciliationRules
{
    public static IReadOnlyList<DivergenciaProposta> Avaliar(
        Relato relato,
        Escola escola,
        IEnumerable<RegistroOficial> registros,
        int anoReferencia)
    {
        var propostas = new List<DivergenciaProposta>();
        var candidatos = registros.Where(r => r.Categoria == relato.Categoria).ToList();

        // INEP: usa apenas o registro mais recente do mesmo item da mesma escola
        var inep = candidatos
            .Where(r => r.Fonte == FonteOficial.Inep
                        && r.EscolaId == escola.Id
                        && r.Item == relato.Item
                        && r.Existe.HasValue)
            .OrderByDescending(r => r.Ano)
            .FirstOrDefault();
        if (inep is not null)
        {
            var p = AvaliarInep(relato, inep);
            if (p is not null) propostas.Add(p);
        }

        foreach (var r in candidatos.Where(r => r.Fonte == FonteOficial.Siope))
        {
            var p = AvaliarSiope(relato, escola, r, anoReferencia);
            if (p is not null) propostas.Add(p);
        }

        return propostas;
    }

    private static DivergenciaProposta? AvaliarInep(Relato relato, RegistroOficial r)
    {
        var item = Catalogo.Rotulo(relato.Categoria, relato.Item);
        var existe = r.Existe!.Value;

        if (existe && relato.Situacao == SituacaoRelatada.Inexistente)
            return new(r.Id, Severidade.Alta,
                $"O Censo Escolar {r.Ano} registra \"{item}\" como existente na escola, mas o relato informa que o item não existe.");

        if (existe && relato.Situacao == SituacaoRelatada.Inadequado)
            return new(r.Id, Severidade.Media,
                $"O Censo Escolar {r.Ano} registra \"{item}\" como existente na escola, mas o relato o descreve em condição inadequada.");

        if (!existe && relato.Situacao == SituacaoRelatada.Adequado)
            return new(r.Id, Severidade.Baixa,
                $"O Censo Escolar {r.Ano} registra \"{item}\" como inexistente na escola, mas o relato informa que existe e está adequado. Possível desatualização da base oficial.");

        return null;
    }

    private static DivergenciaProposta? AvaliarSiope(Relato relato, Escola escola, RegistroOficial r, int anoReferencia)
    {
        // Orçamento só é confrontado com problemas de conservação e mobiliário.
        if (relato.Categoria is not (Categoria.Conservacao or Categoria.Mobiliario)) return null;
        if (relato.Situacao == SituacaoRelatada.Adequado) return null;
        if (r.Valor is not > 0m) return null;
        if (r.Ano < anoReferencia - 1) return null; // só investimento recente
        if (r.Rede != escola.Rede) return null;
        if (!string.Equals(r.Uf, escola.Uf, StringComparison.OrdinalIgnoreCase)) return null;
        if (!string.IsNullOrWhiteSpace(r.Municipio) &&
            !string.Equals(r.Municipio, escola.Municipio, StringComparison.OrdinalIgnoreCase)) return null;

        var local = string.IsNullOrWhiteSpace(r.Municipio) ? escola.Uf : r.Municipio;
        var valor = r.Valor.Value.ToString("0.00", System.Globalization.CultureInfo.InvariantCulture).Replace('.', ',');
        return new(r.Id, Severidade.Baixa,
            $"O SIOPE {r.Ano} registra R$ {valor} de investimento na rede {r.Rede} ({local}) para {relato.Categoria}, " +
            "mas há relato de problema nesta categoria na escola. O orçamento é da rede, não da escola: é preciso verificar a destinação.");
    }
}
