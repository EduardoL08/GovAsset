using GovAsset.Api.Data;
using GovAsset.Api.Domain;
using GovAsset.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace GovAsset.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/admin/divergencias")]
public class DivergenciasController : ControllerBase
{
    private readonly GovAssetDbContext _db;
    private readonly TimeProvider _clock;

    private readonly ILogger<DivergenciasController> _log;

    public DivergenciasController(GovAssetDbContext db, TimeProvider clock, ILogger<DivergenciasController> log)
    {
        _db = db;
        _clock = clock;
        _log = log;
    }

    private IQueryable<Divergencia> Filtrar(int? escolaId, Categoria? categoria, StatusDivergencia? status, Severidade? severidade)
    {
        var q = _db.Divergencias.AsNoTracking().AsQueryable();
        if (escolaId.HasValue) q = q.Where(d => d.EscolaId == escolaId.Value);
        if (categoria.HasValue) q = q.Where(d => d.Categoria == categoria.Value);
        if (status.HasValue) q = q.Where(d => d.Status == status.Value);
        if (severidade.HasValue) q = q.Where(d => d.Severidade == severidade.Value);
        return q;
    }

    /// <summary>Lista paginada de divergências, com filtros por escola e categoria (e status/severidade).</summary>
    [HttpGet]
    public async Task<ActionResult<PaginaDto<DivergenciaResumoDto>>> Listar(
        [FromQuery] int? escolaId, [FromQuery] Categoria? categoria, [FromQuery] StatusDivergencia? status,
        [FromQuery] Severidade? severidade, [FromQuery] int pagina = 1, [FromQuery] int tamanho = 20,
        CancellationToken ct = default)
    {
        pagina = Math.Max(1, pagina);
        tamanho = Math.Clamp(tamanho, 1, 100);
        var q = Filtrar(escolaId, categoria, status, severidade);

        var total = await q.CountAsync(ct);
        var linhas = await q
            .OrderByDescending(d => d.CriadoEmUtc).ThenByDescending(d => d.Id)
            .Skip((pagina - 1) * tamanho).Take(tamanho)
            .Select(d => new DivergenciaResumoDto(
                d.Id, d.RelatoId, d.EscolaId, d.Escola.Nome, d.Escola.CodigoInep, d.Escola.Municipio,
                d.Categoria, d.Item, "", d.Severidade, d.Descricao, d.Status, d.CriadoEmUtc,
                d.Relato.Latitude ?? d.Escola.Latitude, d.Relato.Longitude ?? d.Escola.Longitude))
            .ToListAsync(ct);

        var itens = linhas.Select(x => x with { ItemRotulo = Catalogo.Rotulo(x.Categoria, x.Item) }).ToList();
        return new PaginaDto<DivergenciaResumoDto>(total, pagina, tamanho, itens);
    }

    /// <summary>Pontos para o mapa do painel (até 2000), respeitando os mesmos filtros da lista.</summary>
    [HttpGet("mapa")]
    public async Task<ActionResult<IReadOnlyList<PontoMapaDto>>> Mapa(
        [FromQuery] int? escolaId, [FromQuery] Categoria? categoria, [FromQuery] StatusDivergencia? status,
        [FromQuery] Severidade? severidade, CancellationToken ct)
    {
        var pontos = await Filtrar(escolaId, categoria, status, severidade)
            .Where(d => (d.Relato.Latitude ?? d.Escola.Latitude) != null && (d.Relato.Longitude ?? d.Escola.Longitude) != null)
            .OrderByDescending(d => d.CriadoEmUtc)
            .Take(2000)
            .Select(d => new PontoMapaDto(
                d.Id, (d.Relato.Latitude ?? d.Escola.Latitude)!.Value, (d.Relato.Longitude ?? d.Escola.Longitude)!.Value,
                d.Escola.Nome, d.Categoria, d.Severidade, d.Status))
            .ToListAsync(ct);
        return pontos;
    }

    [HttpGet("{id:int}")]
    public async Task<ActionResult<DivergenciaDetalheDto>> Obter(int id, CancellationToken ct)
    {
        var d = await _db.Divergencias.AsNoTracking()
            .Include(x => x.Escola)
            .Include(x => x.RegistroOficial)
            .Include(x => x.Relato).ThenInclude(r => r.Evidencias)
            .FirstOrDefaultAsync(x => x.Id == id, ct);
        if (d is null) return NotFound();

        var r = d.RegistroOficial;
        return new DivergenciaDetalheDto(
            d.Id, d.RelatoId, d.EscolaId, d.Escola.Nome, d.Escola.CodigoInep, d.Escola.Municipio, d.Escola.Uf,
            d.Categoria, d.Item, Catalogo.Rotulo(d.Categoria, d.Item), d.Severidade, d.Descricao, d.Status,
            d.NotaAnalise, d.CriadoEmUtc, d.AtualizadoEmUtc,
            d.Relato.Situacao, d.Relato.Descricao, d.Relato.CriadoEmUtc,
            d.Relato.Latitude ?? d.Escola.Latitude, d.Relato.Longitude ?? d.Escola.Longitude,
            d.Relato.Evidencias.Select(e => new EvidenciaDto(e.Id, e.ContentType)).ToList(),
            new RegistroOficialDto(r.Fonte, r.Ano, r.Existe, r.Valor, r.Rede, r.Descricao));
    }

    /// <summary>Registra a análise da gestão sobre o indicativo (status e nota).</summary>
    [HttpPatch("{id:int}")]
    public async Task<IActionResult> Atualizar(int id, [FromBody] AtualizarDivergenciaRequest req, CancellationToken ct)
    {
        if (!Enum.IsDefined(req.Status)) return BadRequest(new { mensagem = "Status inválido." });
        var d = await _db.Divergencias.FirstOrDefaultAsync(x => x.Id == id, ct);
        if (d is null) return NotFound();

        d.Status = req.Status;
        d.NotaAnalise = string.IsNullOrWhiteSpace(req.Nota) ? null : req.Nota.Trim();
        d.AtualizadoEmUtc = _clock.GetUtcNow().UtcDateTime;
        await _db.SaveChangesAsync(ct);
        _log.LogInformation("🔎 Divergência #{Id} → {Status} por {Usuario}", id, d.Status,
            User.FindFirst(System.Security.Claims.ClaimTypes.Email)?.Value ?? User.FindFirst("email")?.Value ?? "admin");
        return NoContent();
    }
}

[ApiController]
[Authorize]
[Route("api/admin/resumo")]
public class ResumoController : ControllerBase
{
    private readonly GovAssetDbContext _db;
    public ResumoController(GovAssetDbContext db) => _db = db;

    [HttpGet]
    public async Task<ResumoDto> Obter(CancellationToken ct)
    {
        var totalRelatos = await _db.Relatos.CountAsync(ct);
        var totalEscolas = await _db.Escolas.CountAsync(ct);
        var totalDiv = await _db.Divergencias.CountAsync(ct);

        var porStatus = (await _db.Divergencias.GroupBy(d => d.Status)
                .Select(g => new { g.Key, Total = g.Count() }).ToListAsync(ct))
            .Select(x => new ContagemDto(x.Key.ToString(), x.Total)).ToList();

        var porCategoria = (await _db.Divergencias.GroupBy(d => d.Categoria)
                .Select(g => new { g.Key, Total = g.Count() }).ToListAsync(ct))
            .Select(x => new ContagemDto(x.Key.ToString(), x.Total)).ToList();

        var ranking = await _db.Divergencias
            .GroupBy(d => new { d.EscolaId, d.Escola.Nome, d.Escola.Municipio })
            .Select(g => new EscolaRankingDto(g.Key.EscolaId, g.Key.Nome, g.Key.Municipio, g.Count()))
            .OrderByDescending(x => x.Total).Take(5)
            .ToListAsync(ct);

        return new ResumoDto(totalRelatos, totalEscolas, totalDiv, porStatus, porCategoria, ranking);
    }
}

[ApiController]
[Authorize]
[Route("api/admin/evidencias")]
public class EvidenciasController : ControllerBase
{
    private readonly GovAssetDbContext _db;
    private readonly IEvidenceStorage _storage;

    public EvidenciasController(GovAssetDbContext db, IEvidenceStorage storage)
    {
        _db = db;
        _storage = storage;
    }

    /// <summary>Entrega a foto somente a usuários autenticados (evidências não são públicas).</summary>
    [HttpGet("{id:int}")]
    public async Task<IActionResult> Obter(int id, CancellationToken ct)
    {
        var e = await _db.Evidencias.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id, ct);
        if (e is null) return NotFound();
        var stream = _storage.Abrir(e.Arquivo);
        if (stream is null) return NotFound();

        Response.Headers.CacheControl = "private, no-store";
        return File(stream, e.ContentType);
    }
}

[ApiController]
[Authorize]
[Route("api/admin/importacao")]
public class ImportacaoController : ControllerBase
{
    private const long MaxBytes = 25 * 1024 * 1024;
    private readonly CsvImportService _import;
    private readonly ConciliationService _conciliacao;
    private readonly ILogger<ImportacaoController> _log;

    public ImportacaoController(CsvImportService import, ConciliationService conciliacao, ILogger<ImportacaoController> log)
    {
        _import = import;
        _conciliacao = conciliacao;
        _log = log;
    }

    [HttpPost("escolas")]
    [RequestSizeLimit(26_000_000)]
    public Task<IActionResult> Escolas(IFormFile arquivo, CancellationToken ct) =>
        Executar(arquivo, "escolas", _import.ImportarEscolasAsync, ct);

    [HttpPost("inep")]
    [RequestSizeLimit(26_000_000)]
    public Task<IActionResult> Inep(IFormFile arquivo, CancellationToken ct) =>
        Executar(arquivo, "inep", _import.ImportarInepAsync, ct);

    [HttpPost("siope")]
    [RequestSizeLimit(26_000_000)]
    public Task<IActionResult> Siope(IFormFile arquivo, CancellationToken ct) =>
        Executar(arquivo, "siope", _import.ImportarSiopeAsync, ct);

    /// <summary>Reprocessa todos os relatos contra os registros oficiais atuais.</summary>
    [HttpPost("conciliar")]
    public async Task<IActionResult> Conciliar(CancellationToken ct)
    {
        var novas = await _conciliacao.ConciliarTodosAsync(ct);
        _log.LogInformation("⚖  Conciliação manual: {Novas} divergência(s) nova(s)", novas);
        return Ok(new { novasDivergencias = novas });
    }

    private async Task<IActionResult> Executar(
        IFormFile? arquivo, string tipo, Func<Stream, CancellationToken, Task<ImportResult>> importador, CancellationToken ct)
    {
        if (arquivo is null || arquivo.Length == 0) return BadRequest(new { mensagem = "Envie um arquivo CSV no campo \"arquivo\"." });
        if (arquivo.Length > MaxBytes) return BadRequest(new { mensagem = "Arquivo maior que 25 MB." });
        try
        {
            await using var s = arquivo.OpenReadStream();
            var r = await importador(s, ct);
            _log.LogInformation("📥 Importação {Tipo}: {Ins} inseridos · {Atu} atualizados · {Ign} ignorados · {Err} erro(s)",
                tipo, r.Inseridos, r.Atualizados, r.Ignorados, r.Erros.Count);
            return Ok(r);
        }
        catch (ArgumentException ex)
        {
            _log.LogWarning("📥 Importação {Tipo} recusada: {Motivo}", tipo, ex.Message);
            return BadRequest(new { mensagem = ex.Message });
        }
    }
}
