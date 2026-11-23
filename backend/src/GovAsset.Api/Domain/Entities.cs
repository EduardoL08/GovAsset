namespace GovAsset.Api.Domain;

public class Escola
{
    public int Id { get; set; }
    /// <summary>Código INEP da escola (8 dígitos) — chave de ligação com o Censo Escolar.</summary>
    public string CodigoInep { get; set; } = "";
    public string Nome { get; set; } = "";
    public string Municipio { get; set; } = "";
    public string Uf { get; set; } = "";
    public Rede Rede { get; set; }
    public double? Latitude { get; set; }
    public double? Longitude { get; set; }
}

public class Relato
{
    public int Id { get; set; }
    public int EscolaId { get; set; }
    public Escola Escola { get; set; } = null!;
    public Categoria Categoria { get; set; }
    /// <summary>Chave do item no catálogo (ex.: "rampa"). Ver <see cref="Catalogo"/>.</summary>
    public string Item { get; set; } = "";
    public SituacaoRelatada Situacao { get; set; }
    public string Descricao { get; set; } = "";
    public double? Latitude { get; set; }
    public double? Longitude { get; set; }
    public DateTime CriadoEmUtc { get; set; }
    public List<EvidenciaFotografica> Evidencias { get; set; } = new();
}

public class EvidenciaFotografica
{
    public int Id { get; set; }
    public int RelatoId { get; set; }
    public Relato Relato { get; set; } = null!;
    /// <summary>Nome gerado no armazenamento. O nome original do arquivo NÃO é guardado (minimização, LGPD).</summary>
    public string Arquivo { get; set; } = "";
    public string ContentType { get; set; } = "";
    public long Tamanho { get; set; }
}

/// <summary>
/// Registro oficial importado. Para <see cref="FonteOficial.Inep"/> descreve a existência de um item de
/// infraestrutura numa escola; para <see cref="FonteOficial.Siope"/> descreve um valor de orçamento de uma rede.
/// </summary>
public class RegistroOficial
{
    public int Id { get; set; }
    public FonteOficial Fonte { get; set; }
    public Categoria Categoria { get; set; }
    public int Ano { get; set; }

    // INEP
    public int? EscolaId { get; set; }
    public Escola? Escola { get; set; }
    public string? Item { get; set; }
    public bool? Existe { get; set; }

    // SIOPE
    public Rede? Rede { get; set; }
    public string? Uf { get; set; }
    public string? Municipio { get; set; }
    public decimal? Valor { get; set; }

    public string? Descricao { get; set; }
}

public class Divergencia
{
    public int Id { get; set; }
    public int RelatoId { get; set; }
    public Relato Relato { get; set; } = null!;
    public int EscolaId { get; set; }
    public Escola Escola { get; set; } = null!;
    public int RegistroOficialId { get; set; }
    public RegistroOficial RegistroOficial { get; set; } = null!;
    public Categoria Categoria { get; set; }
    public string Item { get; set; } = "";
    public Severidade Severidade { get; set; }
    public string Descricao { get; set; } = "";
    public StatusDivergencia Status { get; set; } = StatusDivergencia.Aberta;
    public string? NotaAnalise { get; set; }
    public DateTime CriadoEmUtc { get; set; }
    public DateTime? AtualizadoEmUtc { get; set; }
}

public class UsuarioAdmin
{
    public int Id { get; set; }
    public string Email { get; set; } = "";
    public string Nome { get; set; } = "";
    public string SenhaHash { get; set; } = "";
}
