namespace Apropriacao.Domain.Interfaces
{
    /// <summary>
    /// Interface que define a assinatura do plugin de automação do portal.
    /// Esta é uma ferramenta que o Agente IA (Semantic Kernel) pode chamar.
    /// </summary>
    public interface IAutomacaoPortalPlugin
    {
        /// <summary>
        /// Realiza o lançamento de um turno de horas no portal automaticamente.
        /// Esta função será invocada pelo Agente Gemini como uma ferramenta (Tool/Plugin).
        /// </summary>
        /// <param name="cliente">Nome do cliente (ex: "DB Diagnósticos")</param>
        /// <param name="projeto">Nome do projeto/squad (ex: "Squad Toxicológico")</param>
        /// <param name="horaInicio">Horário de início no formato HH:mm (ex: "07:53")</param>
        /// <param name="horaFim">Horário de fim no formato HH:mm (ex: "12:30")</param>
        /// <param name="descricao">Descrição completa da atividade realizada</param>
        /// <returns>Mensagem de sucesso ou erro do lançamento</returns>
        Task<string> LancarHorasAsync(DateTime data, string turno, string cliente, string projeto, string horaInicio, string horaFim, string descricao);
    }
}
