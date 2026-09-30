using Apropriacao.Domain.ValueObjects;

namespace Apropriacao.Domain.Interfaces
{
    /// <summary>
    /// Interface para leitura de arquivos PDF.
    /// Define o contrato que qualquer implementação de leitor de PDF deve respeitar.
    /// </summary>
    public interface IPdfReaderService
    {
        /// <summary>
        /// Extrai todo o texto de um arquivo PDF.
        /// </summary>
        /// <param name="caminhoArquivo">Caminho completo do arquivo PDF</param>
        /// <returns>Value Object contendo o texto extraído e metadados</returns>
        Task<DadosExtraidosArquivo> ExtrairTextoAsync(string caminhoArquivo);
        
        /// <summary>
        /// Extrai texto apenas de uma página específica do PDF.
        /// </summary>
        /// <param name="caminhoArquivo">Caminho completo do arquivo PDF</param>
        /// <param name="numeroPagina">Número da página (1-based)</param>
        /// <returns>Texto extraído da página especificada</returns>
        Task<string> ExtrairTextoPagemAsync(string caminhoArquivo, int numeroPagina);
        
        /// <summary>
        /// Obtém o número total de páginas do PDF.
        /// </summary>
        /// <param name="caminhoArquivo">Caminho completo do arquivo PDF</param>
        /// <returns>Número de páginas</returns>
        Task<int> ObterNumeroPaginasAsync(string caminhoArquivo);
    }
}
