namespace GovAsset.Api.Services;

/// <summary>
/// Abstração do armazenamento de fotos. A implementação local grava em disco; em produção (Render sem disco
/// persistente) troque por um serviço de objetos (S3, Cloudinary, Supabase Storage...) implementando esta interface.
/// </summary>
public interface IEvidenceStorage
{
    Task<string> SalvarAsync(Stream conteudo, string extensao, CancellationToken ct);
    Stream? Abrir(string arquivo);
    void Remover(string arquivo);
}

public class LocalEvidenceStorage : IEvidenceStorage
{
    private readonly string _raiz;

    public LocalEvidenceStorage(IConfiguration cfg, IHostEnvironment env)
    {
        var caminho = cfg["Storage:Path"] ?? "App_Data/evidencias";
        _raiz = Path.IsPathRooted(caminho) ? caminho : Path.Combine(env.ContentRootPath, caminho);
        Directory.CreateDirectory(_raiz);
    }

    public async Task<string> SalvarAsync(Stream conteudo, string extensao, CancellationToken ct)
    {
        var nome = Guid.NewGuid().ToString("N") + extensao;
        await using var destino = new FileStream(Path.Combine(_raiz, nome), FileMode.CreateNew, FileAccess.Write);
        await conteudo.CopyToAsync(destino, ct);
        return nome;
    }

    public Stream? Abrir(string arquivo)
    {
        var caminho = CaminhoSeguro(arquivo);
        return caminho is not null && File.Exists(caminho)
            ? new FileStream(caminho, FileMode.Open, FileAccess.Read, FileShare.Read)
            : null;
    }

    public void Remover(string arquivo)
    {
        var caminho = CaminhoSeguro(arquivo);
        if (caminho is not null && File.Exists(caminho)) File.Delete(caminho);
    }

    // impede path traversal: só aceita um nome de arquivo simples
    private string? CaminhoSeguro(string arquivo) =>
        string.IsNullOrEmpty(arquivo) || Path.GetFileName(arquivo) != arquivo
            ? null
            : Path.Combine(_raiz, arquivo);
}
