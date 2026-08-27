using Amazon.Lambda.APIGatewayEvents;
using Amazon.Lambda.Core;
using System.Text.Json;

[assembly: LambdaSerializer(typeof(Amazon.Lambda.Serialization.SystemTextJson.DefaultLambdaJsonSerializer))]

namespace OficinaMecanica.Auth.Lambda;

public class Function
{
    private readonly IClienteRepository _clienteRepository;
    private readonly JwtGenerator _jwtGenerator;

    // Usado pelo runtime AWS — lê configuração de variáveis de ambiente
    // (connection string e chave JWT injetadas via Terraform, ver README).
    public Function() : this(
        new ClienteRepository(EnvVarObrigatoria("DB_CONNECTION_STRING")),
        new JwtGenerator(
            key: EnvVarObrigatoria("JWT_KEY"),
            issuer: Environment.GetEnvironmentVariable("JWT_ISSUER") ?? "oficina-atendimento",
            audience: Environment.GetEnvironmentVariable("JWT_AUDIENCE") ?? "oficina-atendimento-api",
            expiracaoMinutos: int.Parse(Environment.GetEnvironmentVariable("JWT_EXPIRACAO_MINUTOS") ?? "60")))
    {
    }

    public Function(IClienteRepository clienteRepository, JwtGenerator jwtGenerator)
    {
        _clienteRepository = clienteRepository;
        _jwtGenerator = jwtGenerator;
    }

    public async Task<APIGatewayProxyResponse> FunctionHandler(APIGatewayProxyRequest request, ILambdaContext context)
    {
        var cpf = ExtrairCpf(request);

        if (!CpfValidator.Valido(cpf))
            return Resposta(400, new { erro = "CPF inválido." });

        var documento = CpfValidator.Sanitizar(cpf);
        var cliente = await _clienteRepository.BuscarPorDocumentoAsync(documento, default);

        if (cliente is null)
            return Resposta(404, new { erro = "Cliente não encontrado." });

        if (!cliente.Ativo)
            return Resposta(403, new { erro = "Cliente inativo." });

        var token = _jwtGenerator.Gerar(cliente);
        return Resposta(200, new { token });
    }

    private static string ExtrairCpf(APIGatewayProxyRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Body))
            return "";

        try
        {
            var payload = JsonSerializer.Deserialize<JsonElement>(request.Body);
            return payload.TryGetProperty("cpf", out var cpfProp) ? cpfProp.GetString() ?? "" : "";
        }
        catch (JsonException)
        {
            return "";
        }
    }

    private static APIGatewayProxyResponse Resposta(int statusCode, object body) => new()
    {
        StatusCode = statusCode,
        Body = JsonSerializer.Serialize(body),
        Headers = new Dictionary<string, string> { ["Content-Type"] = "application/json" }
    };

    private static string EnvVarObrigatoria(string nome)
        => Environment.GetEnvironmentVariable(nome) ?? throw new InvalidOperationException($"{nome} não configurado.");
}
