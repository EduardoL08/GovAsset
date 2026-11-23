using GovAsset.Api.Domain;
using GovAsset.Api.Services;
using Xunit;

namespace GovAsset.Tests;

public class ConciliationRulesTests
{
    private static readonly Escola Escola = new()
    {
        Id = 1, CodigoInep = "31900001", Nome = "Escola X", Municipio = "Belo Horizonte", Uf = "MG", Rede = Rede.Municipal,
    };

    private static Relato MontarRelato(Categoria c, string item, SituacaoRelatada s) =>
        new() { Id = 10, EscolaId = 1, Escola = Escola, Categoria = c, Item = item, Situacao = s, Descricao = "descrição de teste" };

    private static RegistroOficial Inep(int id, string item, bool existe, int ano = 2024, Categoria c = Categoria.Acessibilidade) =>
        new() { Id = id, Fonte = FonteOficial.Inep, EscolaId = 1, Categoria = c, Item = item, Existe = existe, Ano = ano };

    private static RegistroOficial Siope(int id, decimal valor, int ano = 2025, string? municipio = "Belo Horizonte",
        Categoria c = Categoria.Conservacao) =>
        new() { Id = id, Fonte = FonteOficial.Siope, Uf = "MG", Municipio = municipio, Rede = Rede.Municipal, Categoria = c, Ano = ano, Valor = valor };

    [Fact]
    public void Oficial_existe_e_relato_inexistente_gera_severidade_alta()
    {
        var r = ConciliationRules.Avaliar(MontarRelato(Categoria.Acessibilidade, "rampa", SituacaoRelatada.Inexistente),
            Escola, new[] { Inep(5, "rampa", true) }, 2026);

        var p = Assert.Single(r);
        Assert.Equal(5, p.RegistroOficialId);
        Assert.Equal(Severidade.Alta, p.Severidade);
    }

    [Fact]
    public void Oficial_existe_e_relato_inadequado_gera_severidade_media()
    {
        var r = ConciliationRules.Avaliar(MontarRelato(Categoria.Acessibilidade, "rampa", SituacaoRelatada.Inadequado),
            Escola, new[] { Inep(5, "rampa", true) }, 2026);

        Assert.Equal(Severidade.Media, Assert.Single(r).Severidade);
    }

    [Fact]
    public void Oficial_inexistente_e_relato_adequado_gera_severidade_baixa()
    {
        var r = ConciliationRules.Avaliar(MontarRelato(Categoria.Acessibilidade, "piso_tatil", SituacaoRelatada.Adequado),
            Escola, new[] { Inep(5, "piso_tatil", false) }, 2026);

        Assert.Equal(Severidade.Baixa, Assert.Single(r).Severidade);
    }

    [Theory]
    [InlineData(true, SituacaoRelatada.Adequado)]
    [InlineData(false, SituacaoRelatada.Inexistente)]
    [InlineData(false, SituacaoRelatada.Inadequado)]
    public void Relato_coerente_com_o_registro_oficial_nao_gera_divergencia(bool existe, SituacaoRelatada situacao)
    {
        var r = ConciliationRules.Avaliar(MontarRelato(Categoria.Acessibilidade, "rampa", situacao),
            Escola, new[] { Inep(5, "rampa", existe) }, 2026);

        Assert.Empty(r);
    }

    [Fact]
    public void Usa_somente_o_registro_inep_mais_recente()
    {
        var registros = new[] { Inep(1, "rampa", true, 2022), Inep(2, "rampa", false, 2024) };

        // relato "inexistente" bate com o registro de 2024 (não existe) -> sem divergência
        var r = ConciliationRules.Avaliar(MontarRelato(Categoria.Acessibilidade, "rampa", SituacaoRelatada.Inexistente),
            Escola, registros, 2026);

        Assert.Empty(r);
    }

    [Fact]
    public void Ignora_registro_de_outra_escola_ou_outro_item()
    {
        var outraEscola = Inep(1, "rampa", true);
        outraEscola.EscolaId = 99;
        var outroItem = Inep(2, "corrimao", true);

        var r = ConciliationRules.Avaliar(MontarRelato(Categoria.Acessibilidade, "rampa", SituacaoRelatada.Inexistente),
            Escola, new[] { outraEscola, outroItem }, 2026);

        Assert.Empty(r);
    }

    [Fact]
    public void Siope_recente_com_relato_de_conservacao_gera_indicativo_baixo()
    {
        var r = ConciliationRules.Avaliar(MontarRelato(Categoria.Conservacao, "telhado", SituacaoRelatada.Inadequado),
            Escola, new[] { Siope(7, 1_250_000m) }, 2026);

        var p = Assert.Single(r);
        Assert.Equal(Severidade.Baixa, p.Severidade);
        Assert.Contains("1250000,00", p.Descricao);
    }

    [Fact]
    public void Siope_nao_se_aplica_a_acessibilidade_nem_a_investimento_antigo_ou_de_outro_municipio()
    {
        var relatoConservacao = MontarRelato(Categoria.Conservacao, "telhado", SituacaoRelatada.Inadequado);

        Assert.Empty(ConciliationRules.Avaliar(
            MontarRelato(Categoria.Acessibilidade, "rampa", SituacaoRelatada.Inexistente),
            Escola, new[] { Siope(1, 100m, c: Categoria.Acessibilidade) }, 2026));

        Assert.Empty(ConciliationRules.Avaliar(relatoConservacao, Escola, new[] { Siope(2, 100m, ano: 2020) }, 2026));
        Assert.Empty(ConciliationRules.Avaliar(relatoConservacao, Escola, new[] { Siope(3, 100m, municipio: "Contagem") }, 2026));
        Assert.Empty(ConciliationRules.Avaliar(relatoConservacao, Escola, new[] { Siope(4, 0m) }, 2026));
    }

    [Fact]
    public void Siope_sem_municipio_vale_para_toda_a_rede_da_uf()
    {
        var r = ConciliationRules.Avaliar(MontarRelato(Categoria.Conservacao, "telhado", SituacaoRelatada.Inexistente),
            Escola, new[] { Siope(1, 500m, municipio: null) }, 2026);

        Assert.Single(r);
    }
}
