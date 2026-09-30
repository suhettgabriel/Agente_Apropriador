namespace Apropriacao.Domain.Entities
{
    /// <summary>
    /// Entidade que representa uma atividade extraída da planilha Excel.
    /// Cada atividade está associada a um turno específico de um dia.
    /// </summary>
    public class AtividadeExcel
    {
        public int Id { get; set; }
        
        /// <summary>
        /// Data em que a atividade foi executada
        /// </summary>
        public DateTime Data { get; set; }
        
        /// <summary>
        /// Turno da atividade: "1º turno", "2º turno", "3º turno", etc.
        /// </summary>
        public string Turno { get; set; } = string.Empty;
        
        /// <summary>
        /// Descrição da tarefa executada
        /// Exemplo: "TASK 76521 - Ajuste API para retornar PDF - Relatório de Requisições Enviadas - Data Lake"
        /// </summary>
        public string Descricao { get; set; } = string.Empty;

        /// <summary>
        /// Projeto/squad ao qual a atividade pertence (ex: "Squad Toxicológico").
        /// </summary>
        public string Projeto { get; set; } = string.Empty;

        /// <summary>
        /// Mês de referência para filtro
        /// </summary>
        public string MesReferencia { get; set; } = string.Empty;
        
        /// <summary>
        /// Indicador de que esta atividade foi processada e lançada no portal
        /// </summary>
        public bool Lancada { get; set; }
        
        public DateTime DataCriacao { get; set; }
        public DateTime? DataLancamento { get; set; }
        
        /// <summary>
        /// Armazena a resposta do agente/portal para referência
        /// </summary>
        public string RespostaLancamento { get; set; } = string.Empty;
    }
}
