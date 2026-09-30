namespace Apropriacao.Application.Commands
{
    /// <summary>
    /// Command para processar a apropriação de horas.
    /// Implementa o padrão CQRS (Command Query Responsibility Segregation).
    /// 
    /// Este comando encapsula os dados necessários para iniciar o processamento
    /// e será enviado para o Handler correspondente.
    /// </summary>
    public class ProcessarApropriacaoCommand
    {
        /// <summary>
        /// Stream do arquivo Excel com atividades
        /// </summary>
        public Stream StreamArquivoPlanilha { get; set; } = Stream.Null;
        
        /// <summary>
        /// Nome do arquivo Excel original
        /// </summary>
        public string NomeArquivoPlanilha { get; set; } = string.Empty;
        
        /// <summary>
        /// Mês de referência para filtrar as atividades
        /// </summary>
        public string MesReferencia { get; set; } = string.Empty;

        public string DiasSelecionados { get; set; } = string.Empty;

        public CancellationToken CancellationToken { get; set; }
        
        public ProcessarApropriacaoCommand()
        {
        }

        public ProcessarApropriacaoCommand(
            Stream streamPlanilha,
            string nomePlanilha,
            string mesReferencia,
            string diasSelecionados,
            CancellationToken cancellationToken = default)
        {
            StreamArquivoPlanilha = streamPlanilha;
            NomeArquivoPlanilha = nomePlanilha;
            MesReferencia = mesReferencia;
            DiasSelecionados = diasSelecionados;
            CancellationToken = cancellationToken;
        }
    }
}
