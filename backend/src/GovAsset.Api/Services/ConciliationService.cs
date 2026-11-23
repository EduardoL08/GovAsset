using GovAsset.Api.Data;
using GovAsset.Api.Domain;
using Microsoft.EntityFrameworkCore;

namespace GovAsset.Api.Services;

/// <summary>Motor de conciliação: aplica <see cref="ConciliationRules"/> e grava as divergências (idempotente).</summary>
public class ConciliationService
{
    private readonly GovAssetDbContext _db;
    private readonly TimeProvider _clock;

    public ConciliationService(GovAssetDbContext db, TimeProvider clock)
    {
        _db = db;
        _clock = clock;
    }

    /// <summary>Concilia um relato. Retorna quantas divergências NOVAS foram gravadas.</summary>
    public async Task<int> ConciliarRelatoAsync(int relatoId, CancellationToken ct = default)
    {
        var novas = await ProcessarAsync(relatoId, ct);
        await _db.SaveChangesAsync(ct);
        return novas;
    }

    /// <summary>Reprocessa todos os relatos (use após importar novos registros oficiais).</summary>
    public async Task<int> ConciliarTodosAsync(CancellationToken ct = default)
    {
        var ids = await _db.Relatos.AsNoTracking().OrderBy(r => r.Id).Select(r => r.Id).ToListAsync(ct);
        var total = 0;
        foreach (var lote in ids.Chunk(200))
        {
            foreach (var id in lote) total += await ProcessarAsync(id, ct);
            await _db.SaveChangesAsync(ct);
            _db.ChangeTracker.Clear();
        }
        return total;
    }

    private async Task<int> ProcessarAsync(int relatoId, CancellationToken ct)
    {
        var relato = await _db.Relatos.Include(r => r.Escola).FirstOrDefaultAsync(r => r.Id == relatoId, ct);
        if (relato is null) return 0;
        var escola = relato.Escola;

        var registros = await _db.RegistrosOficiais.AsNoTracking()
            .Where(r => r.Categoria == relato.Categoria &&
                        ((r.Fonte == FonteOficial.Inep && r.EscolaId == escola.Id && r.Item == relato.Item) ||
                         (r.Fonte == FonteOficial.Siope && r.Uf == escola.Uf)))
            .ToListAsync(ct);

        var propostas = ConciliationRules.Avaliar(relato, escola, registros, _clock.GetUtcNow().Year);
        if (propostas.Count == 0) return 0;

        var jaExistentes = await _db.Divergencias
            .Where(d => d.RelatoId == relato.Id)
            .Select(d => d.RegistroOficialId)
            .ToListAsync(ct);

        var novas = 0;
        foreach (var p in propostas)
        {
            if (jaExistentes.Contains(p.RegistroOficialId)) continue;
            _db.Divergencias.Add(new Divergencia
            {
                RelatoId = relato.Id,
                EscolaId = escola.Id,
                RegistroOficialId = p.RegistroOficialId,
                Categoria = relato.Categoria,
                Item = relato.Item,
                Severidade = p.Severidade,
                Descricao = p.Descricao,
                Status = StatusDivergencia.Aberta,
                CriadoEmUtc = _clock.GetUtcNow().UtcDateTime,
            });
            novas++;
        }
        return novas;
    }
}
