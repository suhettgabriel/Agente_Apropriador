using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using ClosedXML.Excel;
using Apropriacao.Domain.Entities;
using Apropriacao.Domain.Interfaces;
using Apropriacao.Domain.ValueObjects;
using Apropriacao.Domain.Exceptions;

namespace Apropriacao.Infrastructure.Services
{
    /// <summary>
    /// Implementação do serviço de leitura de Excel usando a biblioteca ClosedXML.
    /// Extrai atividades da planilha seguindo o padrão: Data | Turno | Descrição | Projeto
    /// </summary>
    public class ExcelReaderService : IExcelReaderService
    {
        /// <summary>
        /// Extrai todas as atividades da planilha, ignorando a primeira linha (cabeçalho).
        /// Espera-se que a planilha tenha 4 colunas: Data, Turno, Descrição, Projeto
        /// </summary>
        public async Task<List<AtividadeExcel>> ExtrairAtividadesAsync(string caminhoArquivo)
        {
            return await Task.Run(() =>
            {
                if (string.IsNullOrWhiteSpace(caminhoArquivo))
                    throw new ArgumentException("Caminho do arquivo Excel não pode ser vazio.", nameof(caminhoArquivo));

                if (!File.Exists(caminhoArquivo))
                    throw new ArquivoInvalidoException($"Arquivo Excel não encontrado: {caminhoArquivo}");

                try
                {
                    var atividades = new List<AtividadeExcel>();

                    using (var workbook = new XLWorkbook(caminhoArquivo))
                    {
                        // Pega a primeira worksheet (aba)
                        var worksheet = workbook.Worksheets.FirstOrDefault();

                        if (worksheet == null)
                            throw ArquivoInvalidoException.ExcelVazio();

                        var linhasUsadas = worksheet.RangeUsed();

                        if (linhasUsadas == null || linhasUsadas.RowCount() < 2)
                            throw ArquivoInvalidoException.ExcelVazio();

                        // Pula a primeira linha (cabeçalho)
                        var linhas = linhasUsadas.RowsUsed().Skip(1);

                        foreach (var linha in linhas)
                        {
                            try
                            {
                                // Coluna 1: Data
                                var celulaDado = linha.Cell(1);
                                DateTime data = DateTime.MinValue;

                                if (celulaDado.DataType == XLDataType.DateTime)
                                {
                                    data = celulaDado.GetValue<DateTime>();
                                }
                                else if (celulaDado.DataType == XLDataType.Text)
                                {
                                    string valorTexto = celulaDado.GetValue<string>();
                                    if (DateTime.TryParse(valorTexto, out DateTime dataParsed))
                                    {
                                        data = dataParsed;
                                    }
                                }

                                // Coluna 2: Turno
                                string turno = linha.Cell(2).GetValue<string>() ?? string.Empty;
                                turno = turno.Trim();

                                // Coluna 3: Descrição
                                string descricao = linha.Cell(3).GetValue<string>() ?? string.Empty;
                                descricao = descricao.Trim();

                                // Coluna 4: Projeto
                                string projeto = linha.Cell(4).GetValue<string>() ?? string.Empty;
                                projeto = projeto.Trim();

                                // Validar dados antes de adicionar
                                if (data != DateTime.MinValue && !string.IsNullOrWhiteSpace(turno) && !string.IsNullOrWhiteSpace(descricao) && !string.IsNullOrWhiteSpace(projeto))
                                {
                                    var atividade = new AtividadeExcel
                                    {
                                        Data = data,
                                        Turno = turno,
                                        Descricao = descricao,
                                        Projeto = projeto,
                                        MesReferencia = data.ToString("MMMM/yyyy", new CultureInfo("pt-BR")),
                                        Lancada = false,
                                        DataCriacao = DateTime.UtcNow,
                                        RespostaLancamento = string.Empty
                                    };

                                    atividades.Add(atividade);
                                }
                            }
                            catch
                            {
                                // Ignora linhas com dados inválidos e continua processando
                                continue;
                            }
                        }

                        if (atividades.Count == 0)
                            throw ArquivoInvalidoException.ExcelVazio();

                        return atividades;
                    }
                }
                catch (ArquivoInvalidoException)
                {
                    throw;
                }
                catch (Exception ex)
                {
                    throw new ArquivoInvalidoException($"Erro ao extrair atividades do Excel: {ex.Message}", ex);
                }
            });
        }

        /// <summary>
        /// Extrai as atividades filtradas por um mês específico de referência.
        /// </summary>
        public async Task<List<AtividadeExcel>> ExtrairAtividadesPorMesAsync(string caminhoArquivo, string mesReferencia)
        {
            return await Task.Run(async () =>
            {
                var todasAsAtividades = await ExtrairAtividadesAsync(caminhoArquivo);

                if (string.IsNullOrWhiteSpace(mesReferencia))
                    return todasAsAtividades;

                // Normaliza o filtro de mês (ex: "Julho/2026" -> "July/2026" ou "7/2026")
                var atividadesFiltradas = todasAsAtividades
                    .Where(a => a.MesReferencia.Contains(mesReferencia, StringComparison.OrdinalIgnoreCase))
                    .ToList();

                return atividadesFiltradas;
            });
        }

        /// <summary>
        /// Obtém um resumo geral do arquivo Excel (usado para logging e validação).
        /// </summary>
        public async Task<DadosExtraidosArquivo> ObterResumoAsync(string caminhoArquivo)
        {
            return await Task.Run(async () =>
            {
                var atividades = await ExtrairAtividadesAsync(caminhoArquivo);

                var resumoBuilder = new StringBuilder();
                resumoBuilder.AppendLine("=== RESUMO DAS ATIVIDADES EXTRAÍDAS ===");
                resumoBuilder.AppendLine($"Total de atividades: {atividades.Count}");
                resumoBuilder.AppendLine();

                var agrupadasPorData = atividades.GroupBy(a => a.Data.Date);

                foreach (var grupo in agrupadasPorData)
                {
                    resumoBuilder.AppendLine($"Data: {grupo.Key:dd/MM/yyyy}");

                    foreach (var atividade in grupo)
                    {
                        resumoBuilder.AppendLine($"  [{atividade.Turno}] {atividade.Descricao.Substring(0, Math.Min(50, atividade.Descricao.Length))}...");
                    }

                    resumoBuilder.AppendLine();
                }

                return new DadosExtraidosArquivo(
                    conteudo: resumoBuilder.ToString(),
                    tipoArquivo: "Excel",
                    quantidadeLinhas: atividades.Count
                );
            });
        }

        /// <summary>
        /// Filtra uma atividade específica por data e turno.
        /// Retorna null se não encontrar uma atividade correspondente.
        /// </summary>
        public async Task<AtividadeExcel?> ObterAtividadePorDataETurnoAsync(List<AtividadeExcel> atividades, DateTime data, string turno)
        {
            return await Task.Run(() =>
            {
                if (atividades == null || atividades.Count == 0)
                    return null;

                if (string.IsNullOrWhiteSpace(turno))
                    return null;

                var atividade = atividades.FirstOrDefault(a =>
                    a.Data.Date == data.Date &&
                    a.Turno.Equals(turno, StringComparison.OrdinalIgnoreCase));

                return atividade;
            });
        }
    }
}
