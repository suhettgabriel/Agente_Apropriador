namespace Apropriacao.Domain.Exceptions
{
    public class ApropriacaoNegocioException : DomainException
    {
        public ApropriacaoNegocioException(string mensagem) : base(mensagem) { }

        public static ApropriacaoNegocioException RegistroDuplicado(string detalhe)
            => new ApropriacaoNegocioException($"Registro duplicado ou já existente no portal: {detalhe}");

        public static ApropriacaoNegocioException ValidacaoPortal(string detalhe)
            => new ApropriacaoNegocioException($"Validação de negócio do portal falhou: {detalhe}");
    }
}