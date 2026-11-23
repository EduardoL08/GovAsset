namespace GovAsset.Api.Domain;

/// <summary>
/// Catálogo de itens que podem ser relatados por categoria. É o "vocabulário comum" entre os relatos da
/// comunidade e os registros oficiais (o importador do Censo Escolar converte as colunas oficiais para estas chaves).
/// </summary>
public static class Catalogo
{
    public record ItemCatalogo(string Chave, string Rotulo);

    public const string ItemOutro = "outro";

    public static readonly IReadOnlyDictionary<Categoria, IReadOnlyList<ItemCatalogo>> Itens =
        new Dictionary<Categoria, IReadOnlyList<ItemCatalogo>>
        {
            [Categoria.Acessibilidade] = new ItemCatalogo[]
            {
                new("rampa", "Rampa de acesso"),
                new("banheiro_adaptado", "Banheiro adaptado (PNE)"),
                new("piso_tatil", "Piso tátil"),
                new("corrimao", "Corrimão"),
                new("elevador", "Elevador"),
                new(ItemOutro, "Outro item de acessibilidade"),
            },
            [Categoria.Seguranca] = new ItemCatalogo[]
            {
                new("extintor", "Extintor de incêndio"),
                new("saida_emergencia", "Saída de emergência"),
                new("iluminacao_externa", "Iluminação externa"),
                new("muro_cerca", "Muro / cerca / portão"),
                new(ItemOutro, "Outro item de segurança"),
            },
            [Categoria.Conservacao] = new ItemCatalogo[]
            {
                new("telhado", "Telhado / cobertura"),
                new("instalacao_eletrica", "Instalação elétrica"),
                new("instalacao_hidraulica", "Instalação hidráulica"),
                new("banheiros", "Banheiros"),
                new("paredes_pintura", "Paredes / pintura"),
                new(ItemOutro, "Outro problema de conservação"),
            },
            [Categoria.Mobiliario] = new ItemCatalogo[]
            {
                new("carteiras_cadeiras", "Carteiras e cadeiras"),
                new("quadros", "Quadros"),
                new("armarios", "Armários"),
                new("ventiladores", "Ventiladores / climatização"),
                new(ItemOutro, "Outro mobiliário"),
            },
        };

    public static bool ItemValido(Categoria categoria, string? item) =>
        item is not null &&
        Itens.TryGetValue(categoria, out var lista) &&
        lista.Any(i => i.Chave == item);

    public static string Rotulo(Categoria categoria, string item) =>
        Itens.TryGetValue(categoria, out var lista)
            ? lista.FirstOrDefault(i => i.Chave == item)?.Rotulo ?? item
            : item;
}
