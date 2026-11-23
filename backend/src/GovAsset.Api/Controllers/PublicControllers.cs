using GovAsset.Api.Data;
using GovAsset.Api.Domain;
using GovAsset.Api.Security;
using GovAsset.Api.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;

namespace GovAsset.Api.Controllers;

[ApiController]
[Route("api/catalogo")]
public class CatalogoController : ControllerBase
{
    private static readonly Dictionary<Categoria, string> Rotulos = new()
    {
        [Categoria.Acessibilidade] = "Acessibilidade",
        [Categoria.Seguranca] = "Segurança",
        [Categoria.Conservacao] = "Conservação das instalações",
        [Categoria.Mobiliario] = "Mobiliário",
    };

    [HttpGet]
    public IEnumerable<CategoriaCatalogoDto> Listar() =>
        Catalogo.Itens.Select(kv => new CategoriaCatalogoDto(
            kv.Key, Rotulos[kv.Key], kv.Value.Select(i => new ItemCatalogoDto(i.Chave, i.Rotulo)).ToList()));
}

[ApiController]
[Route("api/escolas")]
public class EscolasController : ControllerBase
{
    private readonly GovAssetDbContext _db;
    public EscolasController(GovAssetDbContext db) => _db = db;

    /// <summary>Busca escolas por nome, município ou início do código INEP (mínimo 2 caracteres).</summary>
    [HttpGet]
    public async Task<ActionResult<IEnumerable<EscolaDto>>> Buscar(
        [FromQuery] string? busca, [FromQuery] int limite = 15, CancellationToken ct = default)
    {
        busca = busca?.Trim();
        if (string.IsNullOrEmpty(busca) || busca.Length < 2) return Ok(Array.Empty<EscolaDto>());

        limite = Math.Clamp(limite, 1, 50);
        var padrao = "%" + busca.Replace("\\", "\\\\").Replace("%", "\\%").Replace("_", "\\_") + "%";

        var lista = await _db.Escolas.AsNoTracking()
            .Where(e => EF.Functions.ILike(e.Nome, padrao)
                        || EF.Functions.ILike(e.Municipio, padrao)
                        || e.CodigoInep.StartsWith(busca))
            .OrderBy(e => e.Nome)
            .Take(limite)
            .Select(e => new EscolaDto(e.Id, e.CodigoInep, e.Nome, e.Municipio, e.Uf, e.Rede, e.Latitude, e.Longitude))
            .ToListAsync(ct);
        return Ok(lista);
    }

    [HttpGet("{id:int}")]
    public async Task<ActionResult<EscolaDto>> Obter(int id, CancellationToken ct)
    {
        var e = await _db.Escolas.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id, ct);
        if (e is null) return NotFound();
        return new EscolaDto(e.Id, e.CodigoInep, e.Nome, e.Municipio, e.Uf, e.Rede, e.Latitude, e.Longitude);
    }
}

[ApiController]
[Route("api/relatos")]
public class RelatosController : ControllerBase
{
    private const int MaxFotos = 5;
    private const long MaxBytesPorFoto = 5 * 1024 * 1024;

    private readonly GovAssetDbContext _db;
    private readonly IEvidenceStorage _storage;
    private readonly ConciliationService _conciliacao;
    private readonly TimeProvider _clock;
    private readonly ILogger<RelatosController> _log;

    public RelatosController(GovAssetDbContext db, IEvidenceStorage storage, ConciliationService conciliacao,
        TimeProvider clock, ILogger<RelatosController> log)
    {
        _db = db;
        _storage = storage;
        _conciliacao = conciliacao;
        _clock = clock;
        _log = log;
    }

    /// <summary>
    /// Registra um relato (multipart/form-data). Não coleta nem guarda identificação de quem relata:
    /// apenas os dados necessários ao registro do problema (LGPD, princípio da minimização).
    /// </summary>
    [HttpPost]
    [Consumes("multipart/form-data")]
    [RequestSizeLimit(32_000_000)]
    [EnableRateLimiting("relatos")]
    public async Task<IActionResult> Criar([FromForm] CriarRelatoForm form, CancellationToken ct)
    {
        if (!Enum.IsDefined(form.Categoria)) ModelState.AddModelError(nameof(form.Categoria), "Categoria inválida.");
        if (!Enum.IsDefined(form.Situacao)) ModelState.AddModelError(nameof(form.Situacao), "Situação inválida.");
        if (Enum.IsDefined(form.Categoria) && !Catalogo.ItemValido(form.Categoria, form.Item))
            ModelState.AddModelError(nameof(form.Item), "Item não pertence à categoria informada.");

        var descricao = form.Descricao.Trim();
        if (descricao.Length < 10) ModelState.AddModelError(nameof(form.Descricao), "Descreva o problema com pelo menos 10 caracteres.");

        if (form.Latitude.HasValue != form.Longitude.HasValue)
            ModelState.AddModelError(nameof(form.Latitude), "Informe latitude e longitude juntas.");
        else if (form.Latitude is < -90 or > 90 || form.Longitude is < -180 or > 180)
            ModelState.AddModelError(nameof(form.Latitude), "Coordenadas fora do intervalo válido.");

        if (form.Fotos.Count > MaxFotos)
            ModelState.AddModelError(nameof(form.Fotos), $"Envie no máximo {MaxFotos} fotos.");

        // valida cada foto pelo conteúdo real do arquivo
        var fotos = new List<(IFormFile Arquivo, TipoImagem Tipo)>();
        if (form.Fotos.Count <= MaxFotos)
        {
            foreach (var foto in form.Fotos)
            {
                if (foto.Length == 0 || foto.Length > MaxBytesPorFoto)
                {
                    ModelState.AddModelError(nameof(form.Fotos), "Cada foto deve ter entre 1 byte e 5 MB.");
                    continue;
                }
                var inicio = new byte[ImageValidator.BytesNecessarios];
                await using (var s = foto.OpenReadStream())
                    await s.ReadAtLeastAsync(inicio, 3, throwOnEndOfStream: false, ct);
                var tipo = ImageValidator.Detectar(inicio);
                if (tipo is null) ModelState.AddModelError(nameof(form.Fotos), "Apenas imagens JPEG, PNG ou WEBP são aceitas.");
                else fotos.Add((foto, tipo));
            }
        }

        var escola = await _db.Escolas.AsNoTracking().FirstOrDefaultAsync(e => e.Id == form.EscolaId, ct);
        if (escola is null) ModelState.AddModelError(nameof(form.EscolaId), "Escola não encontrada.");

        if (!ModelState.IsValid) return ValidationProblem(ModelState);

        var relato = new Relato
        {
            EscolaId = escola!.Id,
            Categoria = form.Categoria,
            Item = form.Item,
            Situacao = form.Situacao,
            Descricao = descricao,
            Latitude = form.Latitude,
            Longitude = form.Longitude,
            CriadoEmUtc = _clock.GetUtcNow().UtcDateTime,
        };

        var salvos = new List<string>();
        try
        {
            foreach (var (arquivo, tipo) in fotos)
            {
                await using var s = arquivo.OpenReadStream();
                var nome = await _storage.SalvarAsync(s, tipo.Extensao, ct);
                salvos.Add(nome);
                relato.Evidencias.Add(new EvidenciaFotografica
                {
                    Arquivo = nome, ContentType = tipo.ContentType, Tamanho = arquivo.Length,
                });
            }
            _db.Relatos.Add(relato);
            await _db.SaveChangesAsync(ct);
        }
        catch
        {
            foreach (var nome in salvos) _storage.Remover(nome);
            throw;
        }

        // A conciliação não pode impedir o registro do relato: se falhar, ele pode ser reprocessado pelo painel.
        var novasDivergencias = 0;
        try
        {
            novasDivergencias = await _conciliacao.ConciliarRelatoAsync(relato.Id, ct);
        }
        catch (Exception ex)
        {
            _log.LogError(ex, "Falha ao conciliar o relato {RelatoId}; será reprocessado na próxima execução.", relato.Id);
        }

        // sem dados de quem relatou (LGPD): só o que descreve o problema
        _log.LogInformation("📝 Relato GA-{Ano}-{Id:D6} · {Escola} · {Categoria}/{Item} · {Situacao} · {Fotos} foto(s) · {Div} divergência(s) nova(s)",
            relato.CriadoEmUtc.Year, relato.Id, escola.Nome, relato.Categoria, relato.Item, relato.Situacao,
            relato.Evidencias.Count, novasDivergencias);

        // Quem relata não recebe informação sobre divergências: apenas o protocolo.
        return StatusCode(StatusCodes.Status201Created, new
        {
            id = relato.Id,
            protocolo = $"GA-{relato.CriadoEmUtc:yyyy}-{relato.Id:D6}",
        });
    }
}

[ApiController]
[Route("api/auth")]
public class AuthController : ControllerBase
{
    private readonly GovAssetDbContext _db;
    private readonly TokenService _tokens;
    private readonly ILogger<AuthController> _log;

    public AuthController(GovAssetDbContext db, TokenService tokens, ILogger<AuthController> log)
    {
        _db = db;
        _tokens = tokens;
        _log = log;
    }

    [HttpPost("login")]
    [EnableRateLimiting("login")]
    public async Task<ActionResult<LoginResponse>> Login([FromBody] LoginRequest req, CancellationToken ct)
    {
        var email = req.Email.Trim().ToLowerInvariant();
        var usuario = await _db.Admins.FirstOrDefaultAsync(u => u.Email == email, ct);

        // verifica sempre um hash, mesmo sem usuário, para não revelar por tempo se o e-mail existe
        var ok = Pbkdf2Hasher.Verificar(req.Senha, usuario?.SenhaHash ?? Pbkdf2Hasher.HashFalso);
        if (usuario is null || !ok)
        {
            _log.LogWarning("🔒 Login recusado para {Email}", Mascarar(email));
            return Unauthorized(new { mensagem = "E-mail ou senha inválidos." });
        }

        var t = _tokens.Emitir(usuario);
        _log.LogInformation("🔑 Login no painel: {Email}", usuario.Email);
        return new LoginResponse(t.Token, t.ExpiraEmUtc, usuario.Nome);
    }

    // e-mail digitado errado pode ser de terceiros: no log aparece só "ad***@dominio"
    private static string Mascarar(string email)
    {
        var arroba = email.IndexOf('@');
        if (arroba <= 0) return "***";
        return email[..Math.Min(2, arroba)] + "***" + email[arroba..];
    }
}
