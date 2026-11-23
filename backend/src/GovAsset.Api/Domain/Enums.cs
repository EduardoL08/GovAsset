namespace GovAsset.Api.Domain;

/// <summary>Categorias de relato previstas no objetivo do produto (item 2.2 / 5 do documento).</summary>
public enum Categoria
{
    Acessibilidade = 1,
    Seguranca = 2,
    Conservacao = 3,
    Mobiliario = 4
}

/// <summary>Situação do item segundo quem relata.</summary>
public enum SituacaoRelatada
{
    Inexistente = 1,
    Inadequado = 2,
    Adequado = 3
}

public enum StatusDivergencia
{
    Aberta = 1,
    EmAnalise = 2,
    Confirmada = 3,
    Descartada = 4
}

public enum FonteOficial
{
    Inep = 1,   // Censo Escolar (infraestrutura por escola)
    Siope = 2   // FNDE/SIOPE (orçamento por rede de ensino)
}

public enum Severidade
{
    Baixa = 1,
    Media = 2,
    Alta = 3
}

public enum Rede
{
    Municipal = 1,
    Estadual = 2,
    Federal = 3
}
