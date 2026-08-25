using OficinaMecanica.Auth.Lambda;
using Xunit;

namespace OficinaMecanica.Auth.Lambda.Tests;

public class CpfValidatorTests
{
    [Theory]
    [InlineData("529.982.247-25")]
    [InlineData("52998224725")]
    public void Valido_ComCpfReal_RetornaTrue(string cpf)
    {
        Assert.True(CpfValidator.Valido(cpf));
    }

    [Theory]
    [InlineData("111.111.111-11")]
    [InlineData("000.000.000-00")]
    public void Valido_ComDigitosRepetidos_RetornaFalse(string cpf)
    {
        Assert.False(CpfValidator.Valido(cpf));
    }

    [Theory]
    [InlineData("123.456.789-00")]
    [InlineData("12345678900")]
    public void Valido_ComDigitoVerificadorErrado_RetornaFalse(string cpf)
    {
        Assert.False(CpfValidator.Valido(cpf));
    }

    [Theory]
    [InlineData("123")]
    [InlineData("")]
    [InlineData(null)]
    public void Valido_ComTamanhoInvalido_RetornaFalse(string? cpf)
    {
        Assert.False(CpfValidator.Valido(cpf!));
    }
}
