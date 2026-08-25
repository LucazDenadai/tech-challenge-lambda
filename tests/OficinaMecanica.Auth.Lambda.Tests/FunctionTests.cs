using Amazon.Lambda.APIGatewayEvents;
using Amazon.Lambda.TestUtilities;
using Microsoft.IdentityModel.Tokens;
using OficinaMecanica.Auth.Lambda;
using System.IdentityModel.Tokens.Jwt;
using System.Text;
using System.Text.Json;
using Xunit;

namespace OficinaMecanica.Auth.Lambda.Tests;

public class FakeClienteRepository : IClienteRepository
{
    private readonly Cliente? _cliente;

    public FakeClienteRepository(Cliente? cliente) => _cliente = cliente;

    public Task<Cliente?> BuscarPorDocumentoAsync(string documento, CancellationToken ct)
        => Task.FromResult(_cliente is not null && _cliente.Documento == documento ? _cliente : null);
}

public class FunctionTests
{
    private const string JwtKey = "chave-de-teste-com-pelo-menos-32-caracteres!!";
    private const string CpfValido = "52998224725";

    private static JwtGenerator NovoGerador()
        => new(JwtKey, issuer: "oficina-auth-lambda", audience: "oficina-atendimento-api", expiracaoMinutos: 60);

    private static APIGatewayProxyRequest RequisicaoCom(string? cpf)
        => new() { Body = JsonSerializer.Serialize(new { cpf }) };

    [Fact]
    public async Task FunctionHandler_ComCpfInvalido_Retorna400SemConsultarBanco()
    {
        var repositorio = new FakeClienteRepository(cliente: null);
        var function = new Function(repositorio, NovoGerador());

        var resposta = await function.FunctionHandler(RequisicaoCom("123.456.789-00"), new TestLambdaContext());

        Assert.Equal(400, resposta.StatusCode);
    }

    [Fact]
    public async Task FunctionHandler_ComClienteInexistente_Retorna404()
    {
        var repositorio = new FakeClienteRepository(cliente: null);
        var function = new Function(repositorio, NovoGerador());

        var resposta = await function.FunctionHandler(RequisicaoCom(CpfValido), new TestLambdaContext());

        Assert.Equal(404, resposta.StatusCode);
    }

    [Fact]
    public async Task FunctionHandler_ComClienteInativo_Retorna403()
    {
        var cliente = new Cliente(Guid.NewGuid(), CpfValido, Ativo: false);
        var repositorio = new FakeClienteRepository(cliente);
        var function = new Function(repositorio, NovoGerador());

        var resposta = await function.FunctionHandler(RequisicaoCom(CpfValido), new TestLambdaContext());

        Assert.Equal(403, resposta.StatusCode);
    }

    [Fact]
    public async Task FunctionHandler_ComClienteAtivo_Retorna200ComJwtValido()
    {
        var cliente = new Cliente(Guid.NewGuid(), CpfValido, Ativo: true);
        var repositorio = new FakeClienteRepository(cliente);
        var function = new Function(repositorio, NovoGerador());

        var resposta = await function.FunctionHandler(RequisicaoCom(CpfValido), new TestLambdaContext());

        Assert.Equal(200, resposta.StatusCode);

        var corpo = JsonSerializer.Deserialize<JsonElement>(resposta.Body);
        var token = corpo.GetProperty("token").GetString();
        Assert.False(string.IsNullOrWhiteSpace(token));

        var handler = new JwtSecurityTokenHandler();
        var validationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidIssuer = "oficina-auth-lambda",
            ValidateAudience = true,
            ValidAudience = "oficina-atendimento-api",
            ValidateLifetime = true,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(JwtKey))
        };

        var principal = handler.ValidateToken(token, validationParameters, out _);
        Assert.Equal("Cliente", principal.FindFirst(System.Security.Claims.ClaimTypes.Role)?.Value);
    }
}
