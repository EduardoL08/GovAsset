using GovAsset.Api.Domain;
using Microsoft.EntityFrameworkCore;

namespace GovAsset.Api.Data;

public class GovAssetDbContext : DbContext
{
    public GovAssetDbContext(DbContextOptions<GovAssetDbContext> options) : base(options) { }

    public DbSet<Escola> Escolas => Set<Escola>();
    public DbSet<Relato> Relatos => Set<Relato>();
    public DbSet<EvidenciaFotografica> Evidencias => Set<EvidenciaFotografica>();
    public DbSet<RegistroOficial> RegistrosOficiais => Set<RegistroOficial>();
    public DbSet<Divergencia> Divergencias => Set<Divergencia>();
    public DbSet<UsuarioAdmin> Admins => Set<UsuarioAdmin>();

    protected override void OnModelCreating(ModelBuilder b)
    {
        b.Entity<Escola>(e =>
        {
            e.HasIndex(x => x.CodigoInep).IsUnique();
            e.Property(x => x.CodigoInep).HasMaxLength(8).IsRequired();
            e.Property(x => x.Nome).HasMaxLength(200).IsRequired();
            e.Property(x => x.Municipio).HasMaxLength(120).IsRequired();
            e.Property(x => x.Uf).HasMaxLength(2).IsRequired();
            e.Property(x => x.Rede).HasConversion<string>().HasMaxLength(20);
            e.HasIndex(x => new { x.Uf, x.Municipio });
        });

        b.Entity<Relato>(e =>
        {
            e.Property(x => x.Categoria).HasConversion<string>().HasMaxLength(20);
            e.Property(x => x.Situacao).HasConversion<string>().HasMaxLength(20);
            e.Property(x => x.Item).HasMaxLength(60).IsRequired();
            e.Property(x => x.Descricao).HasMaxLength(2000).IsRequired();
            e.HasOne(x => x.Escola).WithMany().HasForeignKey(x => x.EscolaId).OnDelete(DeleteBehavior.Restrict);
            e.HasIndex(x => new { x.EscolaId, x.Categoria });
        });

        b.Entity<EvidenciaFotografica>(e =>
        {
            e.Property(x => x.Arquivo).HasMaxLength(80).IsRequired();
            e.Property(x => x.ContentType).HasMaxLength(50).IsRequired();
            e.HasOne(x => x.Relato).WithMany(r => r.Evidencias).HasForeignKey(x => x.RelatoId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        b.Entity<RegistroOficial>(e =>
        {
            e.Property(x => x.Fonte).HasConversion<string>().HasMaxLength(20);
            e.Property(x => x.Categoria).HasConversion<string>().HasMaxLength(20);
            e.Property(x => x.Rede).HasConversion<string>().HasMaxLength(20);
            e.Property(x => x.Item).HasMaxLength(60);
            e.Property(x => x.Uf).HasMaxLength(2);
            e.Property(x => x.Municipio).HasMaxLength(120);
            e.Property(x => x.Descricao).HasMaxLength(400);
            e.Property(x => x.Valor).HasPrecision(18, 2);
            e.HasOne(x => x.Escola).WithMany().HasForeignKey(x => x.EscolaId).OnDelete(DeleteBehavior.Cascade);
            e.HasIndex(x => new { x.EscolaId, x.Fonte, x.Categoria, x.Item, x.Ano });
            e.HasIndex(x => new { x.Fonte, x.Uf, x.Categoria, x.Ano });
        });

        b.Entity<Divergencia>(e =>
        {
            e.Property(x => x.Categoria).HasConversion<string>().HasMaxLength(20);
            e.Property(x => x.Severidade).HasConversion<string>().HasMaxLength(20);
            e.Property(x => x.Status).HasConversion<string>().HasMaxLength(20);
            e.Property(x => x.Item).HasMaxLength(60).IsRequired();
            e.Property(x => x.Descricao).HasMaxLength(800).IsRequired();
            e.Property(x => x.NotaAnalise).HasMaxLength(1000);
            e.HasOne(x => x.Relato).WithMany().HasForeignKey(x => x.RelatoId).OnDelete(DeleteBehavior.Cascade);
            e.HasOne(x => x.Escola).WithMany().HasForeignKey(x => x.EscolaId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne(x => x.RegistroOficial).WithMany().HasForeignKey(x => x.RegistroOficialId)
                .OnDelete(DeleteBehavior.Restrict);
            // idempotência do motor de conciliação: um relato gera no máximo uma divergência por registro oficial
            e.HasIndex(x => new { x.RelatoId, x.RegistroOficialId }).IsUnique();
            e.HasIndex(x => new { x.EscolaId, x.Categoria, x.Status });
        });

        b.Entity<UsuarioAdmin>(e =>
        {
            e.HasIndex(x => x.Email).IsUnique();
            e.Property(x => x.Email).HasMaxLength(200).IsRequired();
            e.Property(x => x.Nome).HasMaxLength(120).IsRequired();
            e.Property(x => x.SenhaHash).HasMaxLength(300).IsRequired();
        });
    }
}
