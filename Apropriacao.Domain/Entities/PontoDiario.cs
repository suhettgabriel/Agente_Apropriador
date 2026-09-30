namespace Apropriacao.Domain.Entities
{
    /// <summary>
    /// Entidade que representa um dia de ponto extraído do PDF de espelho de ponto.
    /// Contém os horários de entrada, saída e almoço.
    /// </summary>
    public class PontoDiario
    {
        public int Id { get; set; }
        public DateTime Data { get; set; }
        
        /// <summary>
        /// Horário de entrada da manhã (ex: 07:53)
        /// </summary>
        public TimeSpan EntradaManha { get; set; }
        
        /// <summary>
        /// Horário de saída para almoço (ex: 12:30)
        /// </summary>
        public TimeSpan SaidaManha { get; set; }
        
        /// <summary>
        /// Horário de retorno do almoço (ex: 13:41)
        /// </summary>
        public TimeSpan EntradaTarde { get; set; }
        
        /// <summary>
        /// Horário de saída final (ex: 17:05)
        /// </summary>
        public TimeSpan SaidaTarde { get; set; }
        
        /// <summary>
        /// Mês de referência extraído do PDF (ex: "Julho/2026")
        /// </summary>
        public string MesReferencia { get; set; } = string.Empty;
        
        /// <summary>
        /// Indicador de que este ponto foi processado pelo agente
        /// </summary>
        public bool Processado { get; set; }
        
        public DateTime DataCriacao { get; set; }
        public DateTime? DataProcessamento { get; set; }
    }
}
