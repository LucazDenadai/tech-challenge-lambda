namespace OficinaMecanica.Auth.Lambda;

public interface IClienteRepository
{
    Task<Cliente?> BuscarPorDocumentoAsync(string documento, CancellationToken ct);
}
