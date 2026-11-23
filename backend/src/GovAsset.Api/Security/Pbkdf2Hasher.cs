using System.Security.Cryptography;

namespace GovAsset.Api.Security;

/// <summary>Hash de senha com PBKDF2-SHA256 (formato: v1.iterações.salt.hash, em Base64).</summary>
public static class Pbkdf2Hasher
{
    private const int Iteracoes = 210_000;
    private const int TamSalt = 16;
    private const int TamHash = 32;

    // usado para gastar o mesmo tempo quando o e-mail não existe (evita enumeração de usuários por tempo)
    public static readonly string HashFalso = Hash("senha-falsa-para-igualar-tempo");

    public static string Hash(string senha)
    {
        var salt = RandomNumberGenerator.GetBytes(TamSalt);
        var hash = Rfc2898DeriveBytes.Pbkdf2(senha, salt, Iteracoes, HashAlgorithmName.SHA256, TamHash);
        return $"v1.{Iteracoes}.{Convert.ToBase64String(salt)}.{Convert.ToBase64String(hash)}";
    }

    public static bool Verificar(string senha, string armazenado)
    {
        var partes = armazenado.Split('.');
        if (partes.Length != 4 || partes[0] != "v1" || !int.TryParse(partes[1], out var iter) || iter < 1)
            return false;
        try
        {
            var salt = Convert.FromBase64String(partes[2]);
            var esperado = Convert.FromBase64String(partes[3]);
            var calculado = Rfc2898DeriveBytes.Pbkdf2(senha, salt, iter, HashAlgorithmName.SHA256, esperado.Length);
            return CryptographicOperations.FixedTimeEquals(calculado, esperado);
        }
        catch (FormatException)
        {
            return false;
        }
    }
}
