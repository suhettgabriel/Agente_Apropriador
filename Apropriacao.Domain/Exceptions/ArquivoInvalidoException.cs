namespace Apropriacao.Domain.Exceptions
{
    /// <summary>
    /// Exceção lançada quando um arquivo não atende aos requisitos de processamento.
    /// </summary>
    public class ArquivoInvalidoException : DomainException
    {
        public ArquivoInvalidoException(string mensagem) : base(mensagem) { }
        public ArquivoInvalidoException(string mensagem, Exception innerException) : base(mensagem, innerException) { }
        
        public static ArquivoInvalidoException PdfVazio() 
            => new ArquivoInvalidoException("O arquivo PDF não contém texto extraível. Ele pode estar digitalizado como imagem; gere o espelho em PDF pesquisável ou aplique OCR antes do processamento.");
        
        public static ArquivoInvalidoException ExcelVazio() 
            => new ArquivoInvalidoException("O arquivo Excel está vazio ou não contém atividades válidas.");
        
        public static ArquivoInvalidoException TamanhoExcedido(long tamanhoMaximo) 
            => new ArquivoInvalidoException($"O arquivo excede o tamanho máximo permitido de {tamanhoMaximo} bytes.");
    }
}
