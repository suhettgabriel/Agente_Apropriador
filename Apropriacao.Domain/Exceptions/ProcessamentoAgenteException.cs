namespace Apropriacao.Domain.Exceptions
{
    /// <summary>
    /// Exceção lançada quando o Agente IA falha ao processar ou o Playwright encontra erro.
    /// </summary>
    public class ProcessamentoAgenteException : DomainException
    {
        public ProcessamentoAgenteException(string mensagem) : base(mensagem) { }
        
        public static ProcessamentoAgenteException FalhaPlaywright(string erro) 
            => new ProcessamentoAgenteException($"Erro ao usar Playwright para preencher o portal: {erro}");
        
        public static ProcessamentoAgenteException FalhaGemini(string erro) 
            => new ProcessamentoAgenteException($"Erro ao comunicar com Gemini: {erro}");
    }
}
