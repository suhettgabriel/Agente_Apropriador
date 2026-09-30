using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UglyToad.PdfPig;
using UglyToad.PdfPig.Content;
using UglyToad.PdfPig.DocumentLayoutAnalysis.TextExtractor;
using UglyToad.PdfPig.DocumentLayoutAnalysis.WordExtractor;
using Apropriacao.Domain.Interfaces;
using Apropriacao.Domain.ValueObjects;
using Apropriacao.Domain.Exceptions;

namespace Apropriacao.Infrastructure.Services
{
    /// <summary>
    /// Implementação do serviço de leitura de PDF usando a biblioteca PdfPig.
    /// Extrai texto de todas as páginas ou de páginas específicas.
    /// </summary>
    public class PdfReaderService : IPdfReaderService
    {
        /// <summary>
        /// Extrai todo o texto de um arquivo PDF, varrendo página por página.
        /// </summary>
        public async Task<DadosExtraidosArquivo> ExtrairTextoAsync(string caminhoArquivo)
        {
            return await Task.Run(() =>
            {
                if (string.IsNullOrWhiteSpace(caminhoArquivo))
                    throw new ArgumentException("Caminho do arquivo PDF não pode ser vazio.", nameof(caminhoArquivo));

                if (!File.Exists(caminhoArquivo))
                    throw new ArquivoInvalidoException($"Arquivo PDF não encontrado: {caminhoArquivo}");

                try
                {
                    using (PdfDocument documento = PdfDocument.Open(caminhoArquivo))
                    {
                        var textoCompleto = new StringBuilder();
                        int quantidadePaginas = documento.NumberOfPages;
                        int linhasExtraidas = 0;

                        foreach (var pagina in documento.GetPages())
                        {
                            string textoPagina = ExtrairTextoPaginaComFallback(pagina);
                            
                            if (!string.IsNullOrWhiteSpace(textoPagina))
                            {
                                textoCompleto.AppendLine($"--- PÁGINA {pagina.Number} ---");
                                textoCompleto.AppendLine(textoPagina);
                                textoCompleto.AppendLine();

                                // Conta as linhas extraídas
                                linhasExtraidas += textoPagina.Split(new[] { Environment.NewLine }, StringSplitOptions.None).Length;
                            }
                        }

                        string conteudoFinal = textoCompleto.ToString();

                        if (string.IsNullOrWhiteSpace(conteudoFinal))
                            throw ArquivoInvalidoException.PdfVazio();

                        return new DadosExtraidosArquivo(
                            conteudo: conteudoFinal,
                            tipoArquivo: "PDF",
                            quantidadeLinhas: linhasExtraidas
                        );
                    }
                }
                catch (ArquivoInvalidoException)
                {
                    throw;
                }
                catch (Exception ex)
                {
                    throw new ArquivoInvalidoException($"Erro ao extrair texto do PDF: {ex.Message}", ex);
                }
            });
        }

        /// <summary>
        /// Extrai texto apenas de uma página específica do PDF.
        /// </summary>
        public async Task<string> ExtrairTextoPagemAsync(string caminhoArquivo, int numeroPagina)
        {
            return await Task.Run(() =>
            {
                if (string.IsNullOrWhiteSpace(caminhoArquivo))
                    throw new ArgumentException("Caminho do arquivo PDF não pode ser vazio.", nameof(caminhoArquivo));

                if (numeroPagina < 1)
                    throw new ArgumentException("Número da página deve ser maior que zero.", nameof(numeroPagina));

                if (!File.Exists(caminhoArquivo))
                    throw new ArquivoInvalidoException($"Arquivo PDF não encontrado: {caminhoArquivo}");

                try
                {
                    using (PdfDocument documento = PdfDocument.Open(caminhoArquivo))
                    {
                        if (numeroPagina > documento.NumberOfPages)
                            throw new ArgumentException(
                                $"Página {numeroPagina} não existe. O PDF possui {documento.NumberOfPages} página(s).",
                                nameof(numeroPagina));

                        var pagina = documento.GetPage(numeroPagina);
                        return ExtrairTextoPaginaComFallback(pagina);
                    }
                }
                catch (ArgumentException)
                {
                    throw;
                }
                catch (Exception ex)
                {
                    throw new ArquivoInvalidoException($"Erro ao extrair página {numeroPagina} do PDF: {ex.Message}", ex);
                }
            });
        }

        /// <summary>
        /// Obtém o número total de páginas do PDF.
        /// </summary>
        public async Task<int> ObterNumeroPaginasAsync(string caminhoArquivo)
        {
            return await Task.Run(() =>
            {
                if (string.IsNullOrWhiteSpace(caminhoArquivo))
                    throw new ArgumentException("Caminho do arquivo PDF não pode ser vazio.", nameof(caminhoArquivo));

                if (!File.Exists(caminhoArquivo))
                    throw new ArquivoInvalidoException($"Arquivo PDF não encontrado: {caminhoArquivo}");

                try
                {
                    using (PdfDocument documento = PdfDocument.Open(caminhoArquivo))
                    {
                        return documento.NumberOfPages;
                    }
                }
                catch (Exception ex)
                {
                    throw new ArquivoInvalidoException($"Erro ao contar páginas do PDF: {ex.Message}", ex);
                }
            });
        }

        private static string ExtrairTextoPaginaComFallback(Page pagina)
        {
            var textoDireto = pagina.Text;
            if (!string.IsNullOrWhiteSpace(textoDireto))
                return NormalizarTextoExtraido(textoDireto);

            var textoPorOrdemConteudo = ContentOrderTextExtractor.GetText(pagina, true);
            if (!string.IsNullOrWhiteSpace(textoPorOrdemConteudo))
                return NormalizarTextoExtraido(textoPorOrdemConteudo);

            var palavras = NearestNeighbourWordExtractor.Instance
                .GetWords(pagina.Letters)
                .Select(palavra => palavra.Text)
                .Where(texto => !string.IsNullOrWhiteSpace(texto))
                .ToList();

            if (palavras.Count == 0)
                return string.Empty;

            return NormalizarTextoExtraido(string.Join(" ", palavras));
        }

        private static string NormalizarTextoExtraido(string texto)
        {
            return texto
                .Replace("\r\n", "\n")
                .Replace("\r", "\n")
                .Trim();
        }
    }
}
