using GovAsset.Api.Security;
using GovAsset.Api.Services;
using Xunit;

namespace GovAsset.Tests;

public class SegurancaTests
{
    [Fact]
    public void Hash_de_senha_verifica_corretamente()
    {
        var hash = Pbkdf2Hasher.Hash("Senha@Forte123");

        Assert.True(Pbkdf2Hasher.Verificar("Senha@Forte123", hash));
        Assert.False(Pbkdf2Hasher.Verificar("senha@forte123", hash));
        Assert.NotEqual(hash, Pbkdf2Hasher.Hash("Senha@Forte123")); // salt aleatório
    }

    [Theory]
    [InlineData("")]
    [InlineData("lixo")]
    [InlineData("v1.abc.x.y")]
    [InlineData("v1.1000.!!!.!!!")]
    public void Hash_malformado_nunca_valida(string armazenado)
    {
        Assert.False(Pbkdf2Hasher.Verificar("qualquer", armazenado));
    }

    [Fact]
    public void Detecta_imagens_pelo_conteudo_e_rejeita_o_resto()
    {
        var jpeg = new byte[] { 0xFF, 0xD8, 0xFF, 0xE0, 0, 0, 0, 0, 0, 0, 0, 0 };
        var png = new byte[] { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, 0, 0, 0, 0 };
        var webp = "RIFF\0\0\0\0WEBP"u8.ToArray();
        var exe = new byte[] { 0x4D, 0x5A, 0x90, 0, 0x03, 0, 0, 0, 0x04, 0, 0, 0 };
        var html = "<html><script>"u8.ToArray();

        Assert.Equal("image/jpeg", ImageValidator.Detectar(jpeg)?.ContentType);
        Assert.Equal("image/png", ImageValidator.Detectar(png)?.ContentType);
        Assert.Equal("image/webp", ImageValidator.Detectar(webp)?.ContentType);
        Assert.Null(ImageValidator.Detectar(exe));
        Assert.Null(ImageValidator.Detectar(html));
    }
}
