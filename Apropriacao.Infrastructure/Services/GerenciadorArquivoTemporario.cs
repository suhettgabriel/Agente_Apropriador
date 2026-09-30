using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using Apropriacao.Domain.Interfaces;
using Apropriacao.Domain.ValueObjects;

namespace Apropriacao.Infrastructure.Services
{
    /// <summary>
    /// Implementação do gerenciador de arquivos temporários.
    /// Responsável por salvar arquivos enviados via HTTP na pasta temporária do sistema
    /// e removê-los após o processamento.
    /// </summary>
    public class GerenciadorArquivoTemporario : IGerenciadorArquivoTemporario
    {
        private const string NomePastaAgenteApropriacao = "AgenteApropriacao";

        /// <summary>
        /// Salva um arquivo enviado na pasta temporária do sistema.
        /// Cria um subfolder específico para a aplicação se não existir.
        /// </summary>
        public async Task<ArquivoTemporario> SalvarArquivoTemporarioAsync(Stream stream, string nomeOriginal, string tipoArquivo)
        {
            return await Task.Run(() =>
            {
                if (stream == null)
                    throw new ArgumentNullException(nameof(stream), "Stream não pode ser nulo.");

                if (string.IsNullOrWhiteSpace(nomeOriginal))
                    throw new ArgumentException("Nome original não pode ser vazio.", nameof(nomeOriginal));

                if (tipoArquivo != "PDF" && tipoArquivo != "Excel")
                    throw new ArgumentException("Tipo de arquivo deve ser 'PDF' ou 'Excel'.", nameof(tipoArquivo));

                try
                {
                    // Obter a pasta temporária do sistema
                    string pastaTemp = Path.Combine(Path.GetTempPath(), NomePastaAgenteApropriacao);

                    // Criar a pasta se não existir
                    if (!Directory.Exists(pastaTemp))
                    {
                        Directory.CreateDirectory(pastaTemp);
                    }

                    // Gerar um nome único para o arquivo (evitar colisões)
                    string nomeUnico = $"{DateTime.UtcNow:yyyyMMdd_HHmmss_fff}_{Guid.NewGuid().ToString().Substring(0, 8)}_{nomeOriginal}";
                    string caminhoCompleto = Path.Combine(pastaTemp, nomeUnico);

                    // Salvar o arquivo no disco
                    using (var fileStream = new FileStream(caminhoCompleto, FileMode.Create, FileAccess.Write, FileShare.None))
                    {
                        stream.CopyTo(fileStream);
                        fileStream.Flush();
                    }

                    // Verificar se o arquivo foi criado
                    if (!File.Exists(caminhoCompleto))
                        throw new IOException($"Falha ao criar o arquivo temporário: {caminhoCompleto}");

                    // Obter o tamanho do arquivo
                    var fileInfo = new FileInfo(caminhoCompleto);
                    long tamanhoBytes = fileInfo.Length;

                    // Retornar o Value Object com informações do arquivo
                    return new ArquivoTemporario(caminhoCompleto, nomeOriginal, tipoArquivo, tamanhoBytes);
                }
                catch (Exception ex)
                {
                    throw new IOException($"Erro ao salvar arquivo temporário '{nomeOriginal}': {ex.Message}", ex);
                }
            });
        }

        /// <summary>
        /// Remove um arquivo temporário do disco.
        /// Retorna true se removido com sucesso, false se o arquivo não existir.
        /// </summary>
        public async Task<bool> RemoverArquivoTemporarioAsync(string caminhoArquivo)
        {
            return await Task.Run(() =>
            {
                if (string.IsNullOrWhiteSpace(caminhoArquivo))
                    return false;

                try
                {
                    if (File.Exists(caminhoArquivo))
                    {
                        File.Delete(caminhoArquivo);
                        
                        // Verificar se foi removido
                        if (!File.Exists(caminhoArquivo))
                        {
                            return true;
                        }
                    }

                    return false;
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Erro ao remover arquivo temporário: {ex.Message}");
                    return false;
                }
            });
        }

        /// <summary>
        /// Remove múltiplos arquivos temporários em lote.
        /// Retorna o número de arquivos removidos com sucesso.
        /// </summary>
        public async Task<int> RemoverArquivosTemporarioAsync(List<string> caminhos)
        {
            return await Task.Run(() =>
            {
                if (caminhos == null || caminhos.Count == 0)
                    return 0;

                int removidos = 0;

                foreach (var caminho in caminhos)
                {
                    try
                    {
                        if (File.Exists(caminho))
                        {
                            File.Delete(caminho);

                            // Verificar se foi removido
                            if (!File.Exists(caminho))
                            {
                                removidos++;
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Debug.WriteLine($"Erro ao remover arquivo: {caminho} - {ex.Message}");
                        // Continua tentando remover os próximos arquivos
                        continue;
                    }
                }

                return removidos;
            });
        }

        /// <summary>
        /// Obtém a pasta padrão para arquivos temporários desta aplicação.
        /// </summary>
        public string ObterPastaTemporaria()
        {
            string pastaTemp = Path.Combine(Path.GetTempPath(), NomePastaAgenteApropriacao);

            if (!Directory.Exists(pastaTemp))
            {
                Directory.CreateDirectory(pastaTemp);
            }

            return pastaTemp;
        }
    }
}
