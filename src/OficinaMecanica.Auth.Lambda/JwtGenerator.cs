using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.IdentityModel.Tokens;

namespace OficinaMecanica.Auth.Lambda;

// Compatível com o middleware de auth do Atendimento (JwtTokenService): mesma chave
// simétrica HMAC-SHA256, issuer e audience — só assim o token emitido aqui é aceito
// pelas rotas [Authorize] do Atendimento (validado de ponta a ponta no CARD-30).
public class JwtGenerator
{
    private readonly string _key;
    private readonly string _issuer;
    private readonly string _audience;
    private readonly int _expiracaoMinutos;

    public JwtGenerator(string key, string issuer, string audience, int expiracaoMinutos)
    {
        _key = key;
        _issuer = issuer;
        _audience = audience;
        _expiracaoMinutos = expiracaoMinutos;
    }

    public string Gerar(Cliente cliente)
    {
        var claims = new[]
        {
            new Claim(JwtRegisteredClaimNames.Sub, cliente.Id.ToString()),
            new Claim("documento", cliente.Documento),
            new Claim(ClaimTypes.Role, "Cliente"),
            new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString())
        };

        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_key));
        var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

        var token = new JwtSecurityToken(
            issuer: _issuer,
            audience: _audience,
            claims: claims,
            expires: DateTime.UtcNow.AddMinutes(_expiracaoMinutos),
            signingCredentials: creds);

        return new JwtSecurityTokenHandler().WriteToken(token);
    }
}
