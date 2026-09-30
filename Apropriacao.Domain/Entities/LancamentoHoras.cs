namespace Apropriacao.Domain.Entities
{
    /// <summary>
    /// Entidade consolidada que representa um lançamento de horas no portal.
    /// Resultado do cruzamento entre PontoDiario e AtividadeExcel.
    /// </summary>
    public class LancamentoHoras
    {
        public int Id { get; set; }
        
        /// <summary>
        /// Data do lançamento
        /// </summary>
        public DateTime Data { get; set; }
        
        /// <summary>
        /// Horário de início do turno (HH:mm)
        /// </summary>
        public string HoraInicio { get; set; } = string.Empty;
        
        /// <summary>
        /// Horário de fim do turno (HH:mm)
        /// </summary>
        public string HoraFim { get; set; } = string.Empty;
        
        /// <summary>
        /// Nome do cliente (obtido do contexto ou planilha)
        /// Padrão: "DB Diagnósticos"
        /// </summary>
        public string Cliente { get; set; } = string.Empty;
        
        /// <summary>
        /// Nome do projeto/squad (obtido do contexto ou planilha)
        /// Padrão: "Squad Toxicológico"
        /// </summary>
        public string Projeto { get; set; } = string.Empty;
        
        /// <summary>
        /// Descrição da atividade realizada
        /// </summary>
        public string Descricao { get; set; } = string.Empty;
        
        /// <summary>
        /// Turno identificado: "1º turno" ou "2º turno"
        /// </summary>
        public string Turno { get; set; } = string.Empty;
        
        /// <summary>
        /// Mês de referência
        /// </summary>
        public string MesReferencia { get; set; } = string.Empty;
        
        /// <summary>
        /// Status do lançamento: "Pendente", "Sucesso", "Erro"
        /// </summary>
        public string Status { get; set; } = string.Empty;
        
        /// <summary>
        /// Mensagem de erro ou sucesso do lançamento
        /// </summary>
        public string Mensagem { get; set; } = string.Empty;
        
        /// <summary>
        /// Data em que foi enviado para o portal
        /// </summary>
        public DateTime? DataLancamento { get; set; }
        
        public DateTime DataCriacao { get; set; }
    }
}
