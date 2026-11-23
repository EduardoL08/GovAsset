namespace GovAsset.Api.Services;

public record TipoImagem(string ContentType, string Extensao);

/// <summary>Identifica o tipo real da imagem pelos primeiros bytes (não confia no Content-Type nem na extensão enviados).</summary>
public static class ImageValidator
{
    public const int BytesNecessarios = 12;

    public static TipoImagem? Detectar(ReadOnlySpan<byte> inicio)
    {
        if (inicio.Length >= 3 && inicio[0] == 0xFF && inicio[1] == 0xD8 && inicio[2] == 0xFF)
            return new("image/jpeg", ".jpg");

        if (inicio.Length >= 8 &&
            inicio[0] == 0x89 && inicio[1] == 0x50 && inicio[2] == 0x4E && inicio[3] == 0x47 &&
            inicio[4] == 0x0D && inicio[5] == 0x0A && inicio[6] == 0x1A && inicio[7] == 0x0A)
            return new("image/png", ".png");

        // WEBP: "RIFF" .... "WEBP"
        if (inicio.Length >= 12 &&
            inicio[0] == 'R' && inicio[1] == 'I' && inicio[2] == 'F' && inicio[3] == 'F' &&
            inicio[8] == 'W' && inicio[9] == 'E' && inicio[10] == 'B' && inicio[11] == 'P')
            return new("image/webp", ".webp");

        return null;
    }
}
