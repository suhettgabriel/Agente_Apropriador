namespace Apropriacao.Application.DTOs
{
    /// <summary>
    /// DTO para resposta do processamento de apropriação de horas.
    /// Contém o resultado da execução do agente.
    /// </summary>
    public class ProcessarApropriacaoResponse
    {
        /// <summary>
        /// Indica se o processamento foi bem-sucedido
        /// </summary>
        public bool Sucesso { get; set; }
        
        /// <summary>
        /// Mensagem principal da operação
        /// </summary>
        public string Mensagem { get; set; } = string.Empty;
        
        /// <summary>
        /// Detalhes do processamento realizado pelo agente
        /// </summary>
        public string Detalhes { get; set; } = string.Empty;
        
        /// <summary>
        /// Número de lançamentos realizados com sucesso
        /// </summary>
        public int QuantidadeLancamentosRealizados { get; set; }
        
        /// <summary>
        /// Timestamp de quando a operação foi realizada
        /// </summary>
        public DateTime DataProcessamento { get; set; }
        
        /// <summary>
        /// Mensagens de erro específicas, se houver
        /// </summary>
        public List<string> Erros { get; set; }
        
        public ProcessarApropriacaoResponse()
        {
            Erros = new List<string>();
            DataProcessamento = DateTime.UtcNow;
        }
    }
}
