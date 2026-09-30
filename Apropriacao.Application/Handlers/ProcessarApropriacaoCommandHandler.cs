using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using Apropriacao.Application.Commands;
using Apropriacao.Application.DTOs;
using Apropriacao.Domain.Entities;
using Apropriacao.Domain.Exceptions;
using Apropriacao.Domain.Interfaces;
using Apropriacao.Infrastructure.Plugins;
using Apropriacao.Infrastructure.Services;
using Microsoft.Extensions.Logging;

namespace Apropriacao.Application.Handlers
{
    public class ProcessarApropriacaoCommandHandler
    {
        private readonly IExcelReaderService _excelReaderService;
        private readonly IGerenciadorArquivoTemporario _gerenciadorArquivos;
        private readonly ExtratorPontoTangerinoPlugin _extratorPontoTangerinoPlugin;
        private readonly PortalApropriacaoPlugin _portalPlugin;
        private readonly ILogger<ProcessarApropriacaoCommandHandler> _logger;

        public ProcessarApropriacaoCommandHandler(
            IExcelReaderService excelReaderService,
            IGerenciadorArquivoTemporario gerenciadorArquivos,
            ExtratorPontoTangerinoPlugin extratorPontoTangerinoPlugin,
            PortalApropriacaoPlugin portalPlugin,
            ILogger<ProcessarApropriacaoCommandHandler> logger)
        {
            _excelReaderService = excelReaderService ?? throw new ArgumentNullException(nameof(excelReaderService));
            _gerenciadorArquivos = gerenciadorArquivos ?? throw new ArgumentNullException(nameof(gerenciadorArquivos));
            _extratorPontoTangerinoPlugin = extratorPontoTangerinoPlugin ?? throw new ArgumentNullException(nameof(extratorPontoTangerinoPlugin));
            _portalPlugin = portalPlugin ?? throw new ArgumentNullException(nameof(portalPlugin));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public async Task<ProcessarApropriacaoResponse> HandleAsync(ProcessarApropriacaoCommand command, CancellationToken cancellationToken = default)
        {
            var response = new ProcessarApropriacaoResponse();
            var arquivosTemporarios = new List<string>();
            cancellationToken = cancellationToken == default ? command?.CancellationToken ?? default : cancellationToken;

            try
            {
                cancellationToken.ThrowIfCancellationRequested();
                ValidarCommand(command);
                _logger.LogInformation("[Apropriacao] Início da orquestração determinística Excel + Tangerino + Framework.");

                var arquivoPlanilha = await _gerenciadorArquivos.SalvarArquivoTemporarioAsync(command!.StreamArquivoPlanilha, command.NomeArquivoPlanilha, "Excel");
                arquivosTemporarios.Add(arquivoPlanilha.CaminhoCompleto);
                response.Detalhes = $"Arquivo Excel salvo temporariamente:\n- Excel: {arquivoPlanilha.CaminhoCompleto}\n\n";

                var atividades = await _excelReaderService.ExtrairAtividadesPorMesAsync(arquivoPlanilha.CaminhoCompleto, command.MesReferencia);
                cancellationToken.ThrowIfCancellationRequested();
                var diasSelecionados = NormalizarDiasSelecionados(command.DiasSelecionados, command.MesReferencia);
                atividades = atividades.Where(atividade => diasSelecionados.Contains(atividade.Data.Date)).ToList();

                response.Detalhes += $"Planilha extraída com sucesso:\n- Atividades encontradas: {atividades.Count}\n";
                response.Detalhes += $"Dias selecionados: {string.Join(", ", diasSelecionados.Select(data => data.ToString("dd/MM/yyyy")))}\n\n";

                if (atividades.Count == 0)
                {
                    response.Mensagem = "Nenhuma atividade encontrada para os dias selecionados.";
                    return response;
                }

                _logger.LogInformation("[Apropriacao][Etapa 2.5] Consultando horários do Tangerino diretamente pelo Playwright.");
                var relatorioTangerino = await _extratorPontoTangerinoPlugin.ExtrairPontoAsync(command.MesReferencia, string.Empty);
                cancellationToken.ThrowIfCancellationRequested();

                if (relatorioTangerino.StartsWith("[FALHA_", StringComparison.OrdinalIgnoreCase))
                {
                    response.Mensagem = "Não foi possível extrair os horários do Tangerino.";
                    response.Erros.Add(relatorioTangerino);
                    response.Detalhes += $"=== HORÁRIOS TANGERINO ===\n{relatorioTangerino}\n";
                    return response;
                }

                response.Detalhes += $"=== HORÁRIOS TANGERINO ===\n{relatorioTangerino}\n\n";
                var pontos = ParsearRelatorioTangerino(relatorioTangerino);
                var lancamentos = CriarLancamentos(pontos, atividades, diasSelecionados, command.MesReferencia);
                response.Detalhes += $"Lançamentos candidatos: {lancamentos.Count}\n\n";

                foreach (var lancamento in lancamentos)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    _logger.LogInformation("[Apropriacao] Lançando Data={Data}; Turno={Turno}; Início={Inicio}; Fim={Fim}", lancamento.Data.ToString("dd/MM/yyyy"), lancamento.Turno, lancamento.HoraInicio, lancamento.HoraFim);

                    var resultado = await _portalPlugin.LancarHorasAsync(lancamento.Data, lancamento.Turno, lancamento.Cliente, lancamento.Projeto, lancamento.HoraInicio, lancamento.HoraFim, lancamento.Descricao);
                    lancamento.Mensagem = resultado;
                    lancamento.DataLancamento = DateTime.UtcNow;
                    lancamento.Status = resultado.StartsWith("Sucesso", StringComparison.OrdinalIgnoreCase) ? "Sucesso" : "Erro";
                    response.Detalhes += $"{lancamento.Data:dd/MM/yyyy} - {lancamento.Turno}: {resultado}\n";

                    if (lancamento.Status == "Sucesso")
                        response.QuantidadeLancamentosRealizados++;
                    else
                        response.Erros.Add($"{lancamento.Data:dd/MM/yyyy} - {lancamento.Turno}: {resultado}");
                }

                response.Sucesso = response.QuantidadeLancamentosRealizados > 0 && response.Erros.Count == 0;
                response.Mensagem = response.Sucesso
                    ? $"Apropriação concluída com sucesso! {response.QuantidadeLancamentosRealizados} lançamentos realizados."
                    : $"Processamento concluído parcialmente: {response.QuantidadeLancamentosRealizados} lançamento(s) realizado(s) e {response.Erros.Count} falha(s).";
            }
            catch (OperationCanceledException)
            {
                response.Mensagem = "Processamento cancelado antes da conclusão.";
                response.Erros.Add(response.Mensagem);
                _logger.LogWarning("[Apropriacao] Processamento cancelado.");
            }
            catch (ArquivoInvalidoException ex)
            {
                response.Mensagem = $"Erro ao processar arquivos: {ex.Message}";
                response.Erros.Add(ex.Message);
                _logger.LogWarning(ex, "[Apropriacao] Arquivo inválido.");
            }
            catch (Exception ex)
            {
                response.Mensagem = $"Erro inesperado: {ex.Message}";
                response.Erros.Add(ex.Message);
                response.Detalhes += $"\n\nStack Trace:\n{ex.StackTrace}";
                _logger.LogError(ex, "[Apropriacao] Erro inesperado.");
            }
            finally
            {
                if (arquivosTemporarios.Count > 0)
                {
                    var removidos = await _gerenciadorArquivos.RemoverArquivosTemporarioAsync(arquivosTemporarios);
                    response.Detalhes += $"\nLimpeza: {removidos} arquivo(s) temporário(s) removido(s) com sucesso.";
                }
            }

            return response;
        }

        private static void ValidarCommand(ProcessarApropriacaoCommand? command)
        {
            if (command == null)
                throw new ArgumentNullException(nameof(command));
            if (command.StreamArquivoPlanilha == null || command.StreamArquivoPlanilha.Length == 0)
                throw new ArquivoInvalidoException("Arquivo Excel de atividades não foi enviado ou está vazio.");
            if (string.IsNullOrWhiteSpace(command.MesReferencia))
                throw new ArgumentException("Mês de referência não pode ser vazio.");
            if (string.IsNullOrWhiteSpace(command.DiasSelecionados))
                throw new ArgumentException("Informe pelo menos um dia para lançar.");
        }

        private static List<LancamentoHoras> CriarLancamentos(IReadOnlyDictionary<DateTime, PontoTangerino> pontos, IEnumerable<AtividadeExcel> atividades, ISet<DateTime> diasSelecionados, string mesReferencia)
        {
            var lancamentos = new List<LancamentoHoras>();
            foreach (var atividade in atividades)
            {
                if (!pontos.TryGetValue(atividade.Data.Date, out var ponto))
                    continue;

                var turno = ObterNumeroTurno(atividade.Turno);
                if (turno < 1 || turno > ponto.Turnos.Count)
                    continue;

                var (entrada, saida) = ponto.Turnos[turno - 1];
                if (!EhHorarioValido(entrada))
                    continue;

                // Fallback de saída ausente só faz sentido para o 1º/2º turno (intervalo de almoço/fim do expediente).
                var fallbackSaida = turno switch { 1 => "12:00", 2 => "18:00", _ => null };
                var fim = fallbackSaida is null ? saida : SubstituirSaidaAusente(saida, fallbackSaida);
                if (!EhHorarioValido(fim))
                    continue;

                lancamentos.Add(CriarLancamento(atividade, entrada, fim, $"{turno}º turno", mesReferencia));
            }
            return lancamentos;
        }

        private static LancamentoHoras CriarLancamento(AtividadeExcel atividade, string inicio, string fim, string turno, string mesReferencia)
        {
            return new LancamentoHoras
            {
                Data = atividade.Data.Date,
                HoraInicio = inicio,
                HoraFim = fim,
                Cliente = "DB Diagnósticos",
                Projeto = atividade.Projeto,
                Descricao = atividade.Descricao,
                Turno = turno,
                MesReferencia = mesReferencia,
                Status = "Pendente",
                DataCriacao = DateTime.UtcNow
            };
        }

        private static Dictionary<DateTime, PontoTangerino> ParsearRelatorioTangerino(string relatorio)
        {
            var pontos = new Dictionary<DateTime, PontoTangerino>();
            var linhas = relatorio.Replace("\\n", Environment.NewLine).Split(new[] { "\r\n", "\n", "\r" }, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            foreach (var linha in linhas)
            {
                var colunas = linha.Split('|', StringSplitOptions.TrimEntries);
                if (colunas.Length < 3 || !DateTime.TryParseExact(colunas[0], "dd/MM/yyyy", CultureInfo.InvariantCulture, DateTimeStyles.None, out var data))
                    continue;

                var turnos = new List<(string Entrada, string Saida)>();
                for (var indice = 1; indice + 1 < colunas.Length; indice += 2)
                    turnos.Add((colunas[indice], colunas[indice + 1]));

                pontos[data.Date] = new PontoTangerino(turnos);
            }
            return pontos;
        }

        private static int ObterNumeroTurno(string turno)
        {
            var normalizado = turno.ToLowerInvariant();
            var matchNumero = Regex.Match(normalizado, @"\d+");
            if (matchNumero.Success && int.TryParse(matchNumero.Value, out var numero) && numero > 0)
                return numero;

            if (normalizado.Contains("primeiro") || normalizado.Contains("manha") || normalizado.Contains("manhã")) return 1;
            if (normalizado.Contains("segundo") || normalizado.Contains("tarde")) return 2;
            if (normalizado.Contains("terceiro") || normalizado.Contains("noite")) return 3;
            if (normalizado.Contains("quarto")) return 4;
            if (normalizado.Contains("quinto")) return 5;
            return 0;
        }

        private static bool EhHorarioValido(string horario) => horario != "X" && Regex.IsMatch(horario ?? string.Empty, @"^\d{2}:\d{2}$");
        private static string SubstituirSaidaAusente(string horario, string fallback) => horario.Equals("X", StringComparison.OrdinalIgnoreCase) ? fallback : horario;

        private static HashSet<DateTime> NormalizarDiasSelecionados(string diasSelecionados, string mesReferencia)
        {
            var resultado = new HashSet<DateTime>();
            var (mes, ano) = ExtrairMesAnoReferencia(mesReferencia);
            foreach (var token in diasSelecionados.Split(new[] { ',', ';', '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            {
                if (int.TryParse(token, out var dia) && mes.HasValue && dia is >= 1 and <= 31)
                    resultado.Add(new DateTime(ano, mes.Value, dia));
                else if (DateTime.TryParse(token.Replace('-', '/'), new CultureInfo("pt-BR"), DateTimeStyles.None, out var data))
                    resultado.Add(data.Date);
            }
            return resultado;
        }

        private static (int? Mes, int Ano) ExtrairMesAnoReferencia(string referencia)
        {
            var ano = DateTime.Now.Year;
            var matchAno = Regex.Match(referencia, @"\b(20\d{2})\b");
            if (matchAno.Success) ano = int.Parse(matchAno.Value);
            var meses = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase)
            {
                ["janeiro"] = 1, ["fevereiro"] = 2, ["marco"] = 3, ["março"] = 3, ["abril"] = 4, ["maio"] = 5, ["junho"] = 6,
                ["julho"] = 7, ["agosto"] = 8, ["setembro"] = 9, ["outubro"] = 10, ["novembro"] = 11, ["dezembro"] = 12
            };
            foreach (var mes in meses) if (referencia.Contains(mes.Key, StringComparison.OrdinalIgnoreCase)) return (mes.Value, ano);
            var matchMes = Regex.Match(referencia, @"\b(0?[1-9]|1[0-2])\b");
            return matchMes.Success ? (int.Parse(matchMes.Value), ano) : (null, ano);
        }

        private sealed record PontoTangerino(IReadOnlyList<(string Entrada, string Saida)> Turnos);
    }
}
