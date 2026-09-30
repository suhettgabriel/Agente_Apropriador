using Apropriacao.Domain.Entities;
using Apropriacao.Domain.ValueObjects;

namespace Apropriacao.Domain.Interfaces
{
    /// <summary>
    /// Interface para leitura de arquivos Excel.
    /// Define o contrato que qualquer implementação de leitor de Excel deve respeitar.
    /// </summary>
    public interface IExcelReaderService
    {
        /// <summary>
        /// Extrai as atividades da planilha (ignorando a primeira linha de cabeçalho).
        /// Espera-se que a primeira linha contenha: Data | Turno | Descrição | Projeto
        /// </summary>
        /// <param name="caminhoArquivo">Caminho completo do arquivo Excel</param>
        /// <returns>Lista de AtividadeExcel extraídas</returns>
        Task<List<AtividadeExcel>> ExtrairAtividadesAsync(string caminhoArquivo);
        
        /// <summary>
        /// Extrai as atividades filtradas por mês de referência.
        /// </summary>
        /// <param name="caminhoArquivo">Caminho completo do arquivo Excel</param>
        /// <param name="mesReferencia">Mês de referência (ex: "Julho/2026")</param>
        /// <returns>Lista de AtividadeExcel filtradas</returns>
        Task<List<AtividadeExcel>> ExtrairAtividadesPorMesAsync(string caminhoArquivo, string mesReferencia);
        
        /// <summary>
        /// Obtém um resumo geral do arquivo Excel.
        /// </summary>
        /// <param name="caminhoArquivo">Caminho completo do arquivo Excel</param>
        /// <returns>Value Object com informações de extração</returns>
        Task<DadosExtraidosArquivo> ObterResumoAsync(string caminhoArquivo);
        
        /// <summary>
        /// Filtra atividades por data específica e turno.
        /// </summary>
        /// <param name="atividades">Lista de atividades</param>
        /// <param name="data">Data para filtro</param>
        /// <param name="turno">Turno desejado (ex: "1º turno")</param>
        /// <returns>Atividade correspondente ou null</returns>
        Task<AtividadeExcel?> ObterAtividadePorDataETurnoAsync(List<AtividadeExcel> atividades, DateTime data, string turno);
    }
}
