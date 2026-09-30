namespace Apropriacao.Domain.Exceptions
{
    /// <summary>
    /// Exceção base para todas as exceções de domínio.
    /// Usada para erros relacionados às regras de negócio.
    /// </summary>
    public class DomainException : Exception
    {
        public DomainException(string message) : base(message) { }
        public DomainException(string message, Exception innerException) : base(message, innerException) { }
    }
}
