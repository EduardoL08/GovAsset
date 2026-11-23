using System.ComponentModel.DataAnnotations;
using GovAsset.Api.Domain;

namespace GovAsset.Api;

// ---- públicos
public record EscolaDto(int Id, string CodigoInep, string Nome, string Municipio, string Uf, Rede Rede,
    double? Latitude, double? Longitude);

public record ItemCatalogoDto(string Chave, string Rotulo);
public record CategoriaCatalogoDto(Categoria Categoria, string Rotulo, IReadOnlyList<ItemCatalogoDto> Itens);

public class CriarRelatoForm
{
    public int EscolaId { get; set; }
    public Categoria Categoria { get; set; }

    [Required, StringLength(60)]
    public string Item { get; set; } = "";

    public SituacaoRelatada Situacao { get; set; }

    [Required, StringLength(2000, MinimumLength = 10)]
    public string Descricao { get; set; } = "";

    public double? Latitude { get; set; }
    public double? Longitude { get; set; }
    public List<IFormFile> Fotos { get; set; } = new();
}

public record LoginRequest([Required, EmailAddress, StringLength(200)] string Email,
    [Required, StringLength(200)] string Senha);

public record LoginResponse(string Token, DateTime ExpiraEmUtc, string Nome);

// ---- admin
public record DivergenciaResumoDto(
    int Id, int RelatoId, int EscolaId, string EscolaNome, string CodigoInep, string Municipio,
    Categoria Categoria, string Item, string ItemRotulo, Severidade Severidade, string Descricao,
    StatusDivergencia Status, DateTime CriadoEmUtc, double? Latitude, double? Longitude);

public record PaginaDto<T>(int Total, int Pagina, int Tamanho, IReadOnlyList<T> Itens);

public record PontoMapaDto(int Id, double Latitude, double Longitude, string EscolaNome, Categoria Categoria,
    Severidade Severidade, StatusDivergencia Status);

public record EvidenciaDto(int Id, string ContentType);

public record RegistroOficialDto(FonteOficial Fonte, int Ano, bool? Existe, decimal? Valor, Rede? Rede, string? Descricao);

public record DivergenciaDetalheDto(
    int Id, int RelatoId, int EscolaId, string EscolaNome, string CodigoInep, string Municipio, string Uf,
    Categoria Categoria, string Item, string ItemRotulo, Severidade Severidade, string Descricao,
    StatusDivergencia Status, string? NotaAnalise, DateTime CriadoEmUtc, DateTime? AtualizadoEmUtc,
    SituacaoRelatada SituacaoRelatada, string DescricaoRelato, DateTime RelatoCriadoEmUtc,
    double? Latitude, double? Longitude, IReadOnlyList<EvidenciaDto> Evidencias, RegistroOficialDto RegistroOficial);

public class AtualizarDivergenciaRequest
{
    public StatusDivergencia Status { get; set; }

    [StringLength(1000)]
    public string? Nota { get; set; }
}

public record ContagemDto(string Chave, int Total);
public record EscolaRankingDto(int EscolaId, string Nome, string Municipio, int Total);

public record ResumoDto(int TotalRelatos, int TotalEscolas, int TotalDivergencias,
    IReadOnlyList<ContagemDto> PorStatus, IReadOnlyList<ContagemDto> PorCategoria,
    IReadOnlyList<EscolaRankingDto> EscolasComMaisDivergencias);
