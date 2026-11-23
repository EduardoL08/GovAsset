using System.Security.Claims;
using System.Text;
using GovAsset.Api.Domain;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;

namespace GovAsset.Api.Security;

public record TokenEmitido(string Token, DateTime ExpiraEmUtc);

public class TokenService
{
    private readonly IConfiguration _cfg;
    private readonly TimeProvider _clock;

    public TokenService(IConfiguration cfg, TimeProvider clock)
    {
        _cfg = cfg;
        _clock = clock;
    }

    public TokenEmitido Emitir(UsuarioAdmin usuario)
    {
        var chave = _cfg["Jwt:Key"] ?? throw new InvalidOperationException("Jwt:Key não configurada.");
        var minutos = _cfg.GetValue("Jwt:ExpiraMinutos", 120);
        var expira = _clock.GetUtcNow().UtcDateTime.AddMinutes(minutos);

        var descriptor = new SecurityTokenDescriptor
        {
            Issuer = _cfg["Jwt:Issuer"],
            Audience = _cfg["Jwt:Audience"],
            Expires = expira,
            Subject = new ClaimsIdentity(new[]
            {
                new Claim(JwtRegisteredClaimNames.Sub, usuario.Id.ToString()),
                new Claim(JwtRegisteredClaimNames.Email, usuario.Email),
                new Claim("nome", usuario.Nome),
            }),
            SigningCredentials = new SigningCredentials(
                new SymmetricSecurityKey(Encoding.UTF8.GetBytes(chave)),
                SecurityAlgorithms.HmacSha256),
        };

        return new TokenEmitido(new JsonWebTokenHandler().CreateToken(descriptor), expira);
    }
}
