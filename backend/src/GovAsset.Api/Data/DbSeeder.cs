using GovAsset.Api.Domain;
using GovAsset.Api.Security;
using GovAsset.Api.Services;
using Microsoft.EntityFrameworkCore;

namespace GovAsset.Api.Data;

public static class DbSeeder
{
    public static async Task InicializarAsync(IServiceProvider sp, IConfiguration cfg, ILogger logger)
    {
        using var scope = sp.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<GovAssetDbContext>();

        // Protótipo: cria o esquema se não existir. Para evolução do banco em produção, adote EF Migrations.
        await db.Database.EnsureCreatedAsync();

        await CriarAdminAsync(db, cfg, logger);
        await ImportarDadosReaisAsync(scope.ServiceProvider, db, cfg, logger);
        if (cfg.GetValue<bool>("Seed:Demo")) await CriarDadosDemoAsync(scope.ServiceProvider, db, logger);
    }

    /// <summary>
    /// Na primeira execução (banco sem escolas), importa os CSVs reais de uma pasta (Seed:RealDataPath):
    /// escolas.csv, inep_infraestrutura.csv e, se existir, siope_orcamento.csv. Veja data/real/LEIA-ME.md.
    /// </summary>
    private static async Task ImportarDadosReaisAsync(IServiceProvider sp, GovAssetDbContext db, IConfiguration cfg, ILogger logger)
    {
        var configurada = cfg["Seed:RealDataPath"];
        if (string.IsNullOrWhiteSpace(configurada)) return;

        var raiz = sp.GetRequiredService<IHostEnvironment>().ContentRootPath;
        var pasta = Path.GetFullPath(Path.Combine(raiz, configurada));
        var arqEscolas = Path.Combine(pasta, "escolas.csv");
        if (!File.Exists(arqEscolas))
        {
            logger.LogWarning("Seed:RealDataPath aponta para {Pasta}, mas escolas.csv não foi encontrado.", pasta);
            return;
        }
        if (await db.Escolas.AnyAsync()) return;

        var importador = sp.GetRequiredService<CsvImportService>();

        await using (var s = File.OpenRead(arqEscolas))
        {
            var r = await importador.ImportarEscolasAsync(s, CancellationToken.None);
            logger.LogInformation("🏫 Escolas reais importadas: {Ins} inseridas, {Ign} ignoradas.", r.Inseridos, r.Ignorados);
        }

        var arqInep = Path.Combine(pasta, "inep_infraestrutura.csv");
        if (File.Exists(arqInep))
        {
            await using var s = File.OpenRead(arqInep);
            var r = await importador.ImportarInepAsync(s, CancellationToken.None);
            logger.LogInformation("🧱 Infraestrutura (INEP) importada: {Ins} inseridos, {Ign} ignorados.", r.Inseridos, r.Ignorados);
        }

        var arqSiope = Path.Combine(pasta, "siope_orcamento.csv");
        if (File.Exists(arqSiope))
        {
            await using var s = File.OpenRead(arqSiope);
            var r = await importador.ImportarSiopeAsync(s, CancellationToken.None);
            logger.LogInformation("💰 Orçamento (SIOPE) importado: {Ins} inseridos, {Ign} ignorados.", r.Inseridos, r.Ignorados);
        }
    }

    private static async Task CriarAdminAsync(GovAssetDbContext db, IConfiguration cfg, ILogger logger)
    {
        var email = cfg["Admin:Email"]?.Trim().ToLowerInvariant();
        var senha = cfg["Admin:Senha"];
        if (string.IsNullOrWhiteSpace(email) || string.IsNullOrEmpty(senha))
        {
            logger.LogWarning("Admin:Email / Admin:Senha não configurados: nenhum usuário do painel foi criado.");
            return;
        }
        if (senha.Length < 10)
        {
            logger.LogWarning("Admin:Senha tem menos de 10 caracteres: usuário do painel NÃO criado.");
            return;
        }
        if (await db.Admins.AnyAsync(a => a.Email == email)) return;

        db.Admins.Add(new UsuarioAdmin
        {
            Email = email,
            Nome = cfg["Admin:Nome"] ?? "Administrador",
            SenhaHash = Pbkdf2Hasher.Hash(senha),
        });
        await db.SaveChangesAsync();
        logger.LogInformation("👤 Usuário administrador {Email} criado.", email);
    }

    /// <summary>Dados FICTÍCIOS de demonstração (nomes e códigos inventados) para exercitar o fluxo completo.</summary>
    private static async Task CriarDadosDemoAsync(IServiceProvider sp, GovAssetDbContext db, ILogger logger)
    {
        if (await db.Escolas.AnyAsync()) return;

        var escolas = new[]
        {
            new Escola { CodigoInep = "31900001", Nome = "Escola Municipal Demo Centro", Municipio = "Belo Horizonte", Uf = "MG", Rede = Rede.Municipal, Latitude = -19.9191, Longitude = -43.9386 },
            new Escola { CodigoInep = "31900002", Nome = "Escola Estadual Demo Pampulha", Municipio = "Belo Horizonte", Uf = "MG", Rede = Rede.Estadual, Latitude = -19.8512, Longitude = -43.9667 },
            new Escola { CodigoInep = "31900003", Nome = "Escola Municipal Demo Barreiro", Municipio = "Belo Horizonte", Uf = "MG", Rede = Rede.Municipal, Latitude = -19.9763, Longitude = -44.0231 },
            new Escola { CodigoInep = "31900004", Nome = "Escola Municipal Demo Venda Nova", Municipio = "Belo Horizonte", Uf = "MG", Rede = Rede.Municipal, Latitude = -19.8130, Longitude = -43.9510 },
        };
        db.Escolas.AddRange(escolas);
        await db.SaveChangesAsync();

        var anoCenso = DateTime.UtcNow.Year - 2;
        var anoOrcamento = DateTime.UtcNow.Year - 1;

        RegistroOficial Inep(Escola e, Categoria c, string item, bool existe) => new()
        {
            Fonte = FonteOficial.Inep, EscolaId = e.Id, Categoria = c, Item = item, Existe = existe, Ano = anoCenso,
        };

        db.RegistrosOficiais.AddRange(
            Inep(escolas[0], Categoria.Acessibilidade, "rampa", true),
            Inep(escolas[0], Categoria.Acessibilidade, "piso_tatil", false),
            Inep(escolas[0], Categoria.Seguranca, "extintor", true),
            Inep(escolas[1], Categoria.Acessibilidade, "rampa", true),
            Inep(escolas[1], Categoria.Acessibilidade, "piso_tatil", false),
            Inep(escolas[2], Categoria.Acessibilidade, "banheiro_adaptado", true),
            Inep(escolas[2], Categoria.Conservacao, "telhado", true),
            Inep(escolas[3], Categoria.Mobiliario, "carteiras_cadeiras", true),
            new RegistroOficial { Fonte = FonteOficial.Siope, Uf = "MG", Municipio = "Belo Horizonte", Rede = Rede.Municipal, Categoria = Categoria.Mobiliario, Ano = anoOrcamento, Valor = 380000m, Descricao = "Aquisição de mobiliário (demo)" },
            new RegistroOficial { Fonte = FonteOficial.Siope, Uf = "MG", Municipio = "Belo Horizonte", Rede = Rede.Municipal, Categoria = Categoria.Conservacao, Ano = anoOrcamento, Valor = 1250000m, Descricao = "Manutenção predial (demo)" });
        await db.SaveChangesAsync();

        Relato R(Escola e, Categoria c, string item, SituacaoRelatada s, string texto, double dLat, double dLon) => new()
        {
            EscolaId = e.Id, Categoria = c, Item = item, Situacao = s, Descricao = texto,
            Latitude = e.Latitude + dLat, Longitude = e.Longitude + dLon,
            CriadoEmUtc = DateTime.UtcNow.AddDays(-Random.Shared.Next(1, 20)),
        };

        db.Relatos.AddRange(
            R(escolas[0], Categoria.Acessibilidade, "rampa", SituacaoRelatada.Inexistente, "A rampa da entrada principal foi removida na última obra e não foi refeita. Cadeirantes não conseguem entrar.", 0.0002, 0.0001),
            R(escolas[0], Categoria.Seguranca, "extintor", SituacaoRelatada.Inadequado, "Os extintores do corredor estão com a validade vencida há mais de um ano.", -0.0001, 0.0002),
            R(escolas[1], Categoria.Acessibilidade, "piso_tatil", SituacaoRelatada.Adequado, "Há piso tátil em todo o pátio e na entrada, em bom estado.", 0.0001, -0.0002),
            R(escolas[1], Categoria.Acessibilidade, "rampa", SituacaoRelatada.Adequado, "A rampa está em ótimo estado e com corrimão dos dois lados.", 0.0002, 0.0002),
            R(escolas[2], Categoria.Conservacao, "telhado", SituacaoRelatada.Inadequado, "Goteiras em três salas quando chove; o forro já caiu em uma delas.", -0.0002, 0.0001),
            R(escolas[2], Categoria.Acessibilidade, "banheiro_adaptado", SituacaoRelatada.Inexistente, "Não existe banheiro adaptado; o que tinha virou depósito.", 0.0001, 0.0001),
            R(escolas[3], Categoria.Mobiliario, "carteiras_cadeiras", SituacaoRelatada.Inadequado, "Muitas carteiras quebradas e cadeiras sem encosto; alunos estudam em pé em algumas aulas.", 0.0, -0.0001));
        await db.SaveChangesAsync();

        var novas = await sp.GetRequiredService<ConciliationService>().ConciliarTodosAsync();
        logger.LogInformation("🧪 Dados de demonstração criados ({Divergencias} divergências geradas).", novas);
    }
}
