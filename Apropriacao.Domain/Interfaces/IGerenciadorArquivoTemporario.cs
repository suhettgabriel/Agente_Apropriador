using Apropriacao.Domain.ValueObjects;

namespace Apropriacao.Domain.Interfaces
{
    /// <summary>
    /// Interface para gerenciamento de arquivos temporários.
    /// Responsável por salvar, carregar e limpar arquivos enviados.
    /// </summary>
    public interface IGerenciadorArquivoTemporario
    {
        /// <summary>
        /// Salva um arquivo enviado na pasta temporária do sistema.
        /// </summary>
        /// <param name="stream">Stream do arquivo</param>
        /// <param name="nomeOriginal">Nome original do arquivo</param>
        /// <param name="tipoArquivo">Tipo ("PDF" ou "Excel")</param>
        /// <returns>Value Object com informações do arquivo salvo</returns>
        Task<ArquivoTemporario> SalvarArquivoTemporarioAsync(Stream stream, string nomeOriginal, string tipoArquivo);
        
        /// <summary>
        /// Remove um arquivo temporário do disco.
        /// </summary>
        /// <param name="caminhoArquivo">Caminho completo do arquivo</param>
        /// <returns>True se removido com sucesso</returns>
        Task<bool> RemoverArquivoTemporarioAsync(string caminhoArquivo);
        
        /// <summary>
        /// Remove múltiplos arquivos temporários.
        /// </summary>
        /// <param name="caminhos">Lista de caminhos dos arquivos</param>
        /// <returns>Número de arquivos removidos com sucesso</returns>
        Task<int> RemoverArquivosTemporarioAsync(List<string> caminhos);
        
        /// <summary>
        /// Obtém a pasta padrão para arquivos temporários.
        /// </summary>
        /// <returns>Caminho da pasta temporária</returns>
        string ObterPastaTemporaria();
    }
}
