using Npgsql;

namespace OficinaMecanica.Auth.Lambda;

// Consulta direta ao RDS via Npgsql — sem EF Core, por peso/cold start (card CARD-29).
public class ClienteRepository : IClienteRepository
{
    private readonly string _connectionString;

    public ClienteRepository(string connectionString) => _connectionString = connectionString;

    public async Task<Cliente?> BuscarPorDocumentoAsync(string documento, CancellationToken ct)
    {
        await using var conn = new NpgsqlConnection(_connectionString);
        await conn.OpenAsync(ct);

        await using var cmd = new NpgsqlCommand(
            "SELECT \"Id\", \"Documento\", \"Ativo\" FROM atendimento.\"Clientes\" WHERE \"Documento\" = $1",
            conn);
        cmd.Parameters.AddWithValue(documento);

        await using var reader = await cmd.ExecuteReaderAsync(ct);
        if (!await reader.ReadAsync(ct))
            return null;

        return new Cliente(reader.GetGuid(0), reader.GetString(1), reader.GetBoolean(2));
    }
}
