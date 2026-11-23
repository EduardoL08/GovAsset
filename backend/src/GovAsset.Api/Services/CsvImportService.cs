using System.Globalization;
using System.Text;
using CsvHelper;
using CsvHelper.Configuration;
using GovAsset.Api.Data;
using GovAsset.Api.Domain;
using Microsoft.EntityFrameworkCore;

namespace GovAsset.Api.Services;

public record ImportResult(int Inseridos, int Atualizados, int Ignorados, List<string> Erros);

/// <summary>
/// Rotina de importação de dados abertos. Lê CSVs (separador "," ou ";", UTF-8) no formato GovAsset:
///  - escolas: codigo_inep,nome,municipio,uf,rede,latitude,longitude
///  - inep:    codigo_inep,categoria,item,existe,ano          (infraestrutura do Censo Escolar, formato longo)
///  - siope:   ano,uf,municipio,rede,categoria,valor,descricao (orçamento por rede de ensino)
/// Os arquivos brutos do INEP/FNDE devem ser convertidos para este layout (ver README).
/// </summary>
public class CsvImportService
{
    private const int MaxErros = 50;
    private readonly GovAssetDbContext _db;

    public CsvImportService(GovAssetDbContext db) => _db = db;

    // ---------------------------------------------------------------- escolas
    public async Task<ImportResult> ImportarEscolasAsync(Stream arquivo, CancellationToken ct)
    {
        using var csv = Abrir(arquivo, "codigo_inep", "nome", "municipio", "uf", "rede");
        var existentes = await _db.Escolas.ToDictionaryAsync(e => e.CodigoInep, ct);
        int ins = 0, upd = 0, ign = 0;
        var erros = new List<string>();

        while (await csv.ReadAsync())
        {
            var linha = csv.Parser.Row;
            var codigo = SomenteDigitos(Campo(csv, "codigo_inep"));
            var nome = Campo(csv, "nome");
            var municipio = Campo(csv, "municipio");
            var uf = Campo(csv, "uf")?.ToUpperInvariant();

            if (codigo.Length != 8) { Erro(erros, linha, "codigo_inep deve ter 8 dígitos."); ign++; continue; }
            if (string.IsNullOrWhiteSpace(nome) || string.IsNullOrWhiteSpace(municipio)) { Erro(erros, linha, "nome e municipio são obrigatórios."); ign++; continue; }
            if (uf is null || uf.Length != 2) { Erro(erros, linha, "uf deve ter 2 letras."); ign++; continue; }
            if (!TentarRede(Campo(csv, "rede"), out var rede)) { Erro(erros, linha, "rede inválida (Municipal, Estadual ou Federal)."); ign++; continue; }

            var lat = Numero(Campo(csv, "latitude"));
            var lon = Numero(Campo(csv, "longitude"));
            if ((lat is < -90 or > 90) || (lon is < -180 or > 180)) { lat = null; lon = null; }

            if (existentes.TryGetValue(codigo, out var e))
            {
                e.Nome = Cortar(nome, 200); e.Municipio = Cortar(municipio, 120); e.Uf = uf; e.Rede = rede;
                e.Latitude = lat; e.Longitude = lon;
                upd++;
            }
            else
            {
                var nova = new Escola
                {
                    CodigoInep = codigo, Nome = Cortar(nome, 200), Municipio = Cortar(municipio, 120),
                    Uf = uf, Rede = rede, Latitude = lat, Longitude = lon,
                };
                _db.Escolas.Add(nova);
                existentes[codigo] = nova;
                ins++;
            }
        }

        await _db.SaveChangesAsync(ct);
        return new ImportResult(ins, upd, ign, erros);
    }

    // ---------------------------------------------------------------- INEP
    public async Task<ImportResult> ImportarInepAsync(Stream arquivo, CancellationToken ct)
    {
        using var csv = Abrir(arquivo, "codigo_inep", "categoria", "item", "existe", "ano");
        var escolas = await _db.Escolas.AsNoTracking().ToDictionaryAsync(e => e.CodigoInep, e => e.Id, ct);
        var existentes = await _db.RegistrosOficiais
            .Where(r => r.Fonte == FonteOficial.Inep && r.EscolaId != null && r.Item != null)
            .ToDictionaryAsync(r => (r.EscolaId!.Value, r.Categoria, r.Item!, r.Ano), ct);
        int ins = 0, upd = 0, ign = 0;
        var erros = new List<string>();

        while (await csv.ReadAsync())
        {
            var linha = csv.Parser.Row;
            var codigo = SomenteDigitos(Campo(csv, "codigo_inep"));
            if (!escolas.TryGetValue(codigo, out var escolaId)) { Erro(erros, linha, $"escola {codigo} não cadastrada (importe as escolas antes)."); ign++; continue; }
            if (!TentarCategoria(Campo(csv, "categoria"), out var categoria)) { Erro(erros, linha, "categoria inválida."); ign++; continue; }
            var item = Normalizar(Campo(csv, "item"));
            if (!Catalogo.ItemValido(categoria, item)) { Erro(erros, linha, $"item \"{item}\" não existe no catálogo da categoria {categoria}."); ign++; continue; }
            if (!TentarBool(Campo(csv, "existe"), out var existe)) { Erro(erros, linha, "existe deve ser sim/nao, true/false ou 1/0."); ign++; continue; }
            if (!int.TryParse(Campo(csv, "ano"), out var ano) || ano is < 2000 or > 2100) { Erro(erros, linha, "ano inválido."); ign++; continue; }

            var chave = (escolaId, categoria, item, ano);
            if (existentes.TryGetValue(chave, out var r))
            {
                r.Existe = existe;
                upd++;
            }
            else
            {
                var novo = new RegistroOficial
                {
                    Fonte = FonteOficial.Inep, EscolaId = escolaId, Categoria = categoria,
                    Item = item, Existe = existe, Ano = ano,
                };
                _db.RegistrosOficiais.Add(novo);
                existentes[chave] = novo;
                ins++;
            }
        }

        await _db.SaveChangesAsync(ct);
        return new ImportResult(ins, upd, ign, erros);
    }

    // ---------------------------------------------------------------- SIOPE
    public async Task<ImportResult> ImportarSiopeAsync(Stream arquivo, CancellationToken ct)
    {
        using var csv = Abrir(arquivo, "ano", "uf", "rede", "categoria", "valor");
        var existentes = await _db.RegistrosOficiais
            .Where(r => r.Fonte == FonteOficial.Siope && r.Uf != null && r.Rede != null)
            .ToDictionaryAsync(r => (r.Uf!, r.Municipio ?? "", r.Rede!.Value, r.Categoria, r.Ano), ct);
        int ins = 0, upd = 0, ign = 0;
        var erros = new List<string>();

        while (await csv.ReadAsync())
        {
            var linha = csv.Parser.Row;
            var uf = Campo(csv, "uf")?.ToUpperInvariant();
            if (uf is null || uf.Length != 2) { Erro(erros, linha, "uf deve ter 2 letras."); ign++; continue; }
            if (!int.TryParse(Campo(csv, "ano"), out var ano) || ano is < 2000 or > 2100) { Erro(erros, linha, "ano inválido."); ign++; continue; }
            if (!TentarRede(Campo(csv, "rede"), out var rede)) { Erro(erros, linha, "rede inválida."); ign++; continue; }
            if (!TentarCategoria(Campo(csv, "categoria"), out var categoria)) { Erro(erros, linha, "categoria inválida."); ign++; continue; }
            var valor = ParseDecimal(Campo(csv, "valor"));
            if (valor is null or < 0) { Erro(erros, linha, "valor inválido."); ign++; continue; }

            var municipio = Campo(csv, "municipio") ?? "";
            var descricao = Campo(csv, "descricao");
            var chave = (uf, municipio, rede, categoria, ano);

            if (existentes.TryGetValue(chave, out var r))
            {
                r.Valor = valor; r.Descricao = descricao is null ? null : Cortar(descricao, 400);
                upd++;
            }
            else
            {
                var novo = new RegistroOficial
                {
                    Fonte = FonteOficial.Siope, Uf = uf, Municipio = municipio == "" ? null : Cortar(municipio, 120),
                    Rede = rede, Categoria = categoria, Ano = ano, Valor = valor,
                    Descricao = descricao is null ? null : Cortar(descricao, 400),
                };
                _db.RegistrosOficiais.Add(novo);
                existentes[chave] = novo;
                ins++;
            }
        }

        await _db.SaveChangesAsync(ct);
        return new ImportResult(ins, upd, ign, erros);
    }

    // ---------------------------------------------------------------- utilitários
    private static CsvReader Abrir(Stream s, params string[] obrigatorias)
    {
        var reader = new StreamReader(s, Encoding.UTF8, true);
        var csv = new CsvReader(reader, new CsvConfiguration(CultureInfo.InvariantCulture)
        {
            DetectDelimiter = true,
            DetectDelimiterValues = new[] { ",", ";" },
            MissingFieldFound = null,
            BadDataFound = null,
            HeaderValidated = null,
            PrepareHeaderForMatch = args => Normalizar(args.Header),
        });

        if (!csv.Read() || !csv.ReadHeader())
        {
            csv.Dispose();
            throw new ArgumentException("O arquivo está vazio.");
        }

        var cabecalho = (csv.HeaderRecord ?? Array.Empty<string>()).Select(Normalizar).ToHashSet();
        var faltando = obrigatorias.Where(o => !cabecalho.Contains(o)).ToList();
        if (faltando.Count > 0)
        {
            csv.Dispose();
            throw new ArgumentException("Colunas obrigatórias ausentes: " + string.Join(", ", faltando) + ".");
        }
        return csv;
    }

    private static string? Campo(CsvReader csv, string nome) =>
        csv.TryGetField<string>(nome, out var v) && !string.IsNullOrWhiteSpace(v) ? v.Trim() : null;

    private static void Erro(List<string> erros, int linha, string msg)
    {
        if (erros.Count < MaxErros) erros.Add($"Linha {linha}: {msg}");
    }

    internal static string Normalizar(string? s)
    {
        if (string.IsNullOrWhiteSpace(s)) return "";
        var d = s.Trim().Normalize(NormalizationForm.FormD);
        var sb = new StringBuilder(d.Length);
        foreach (var c in d)
            if (CharUnicodeInfo.GetUnicodeCategory(c) != UnicodeCategory.NonSpacingMark) sb.Append(c);
        return sb.ToString().Normalize(NormalizationForm.FormC).ToLowerInvariant().Replace(' ', '_');
    }

    private static string Cortar(string s, int max) => s.Length <= max ? s : s[..max];

    private static string SomenteDigitos(string? s) => new((s ?? "").Where(char.IsDigit).ToArray());

    private static bool TentarCategoria(string? s, out Categoria c) =>
        Enum.TryParse(Normalizar(s), true, out c) && Enum.IsDefined(c);

    private static bool TentarRede(string? s, out Rede r) =>
        Enum.TryParse(Normalizar(s), true, out r) && Enum.IsDefined(r);

    private static bool TentarBool(string? s, out bool v)
    {
        switch (Normalizar(s))
        {
            case "sim" or "s" or "1" or "true" or "x": v = true; return true;
            case "nao" or "n" or "0" or "false": v = false; return true;
            default: v = false; return false;
        }
    }

    private static double? Numero(string? s) =>
        s is not null && double.TryParse(s.Replace(',', '.'), NumberStyles.Float, CultureInfo.InvariantCulture, out var v)
            ? v : null;

    private static decimal? ParseDecimal(string? s)
    {
        if (s is null) return null;
        // aceita "1.234,56" (pt-BR) e "1234.56" (invariante), sem depender de cultura instalada
        var texto = s.Contains(',') ? s.Replace(".", "").Replace(',', '.') : s;
        return decimal.TryParse(texto, NumberStyles.Number, CultureInfo.InvariantCulture, out var v) ? v : null;
    }
}
