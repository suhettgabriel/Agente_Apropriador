using System.ComponentModel;
using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using Apropriacao.Domain.Exceptions;
using Apropriacao.Domain.Options;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.Playwright;
using Microsoft.SemanticKernel;

namespace Apropriacao.Infrastructure.Plugins
{
    public class ExtratorPontoTangerinoPlugin
    {
        private readonly TangerinoOptions _options;
        private readonly ILogger<ExtratorPontoTangerinoPlugin> _logger;
        private IBrowser? _browser;
        private IPage? _page;
        private bool _navegadorIniciado;

        public ExtratorPontoTangerinoPlugin(
            IOptions<TangerinoOptions> options,
            ILogger<ExtratorPontoTangerinoPlugin> logger)
        {
            _options = options.Value ?? throw new ArgumentNullException(nameof(options));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        [KernelFunction("ExtrairPontoTangerino")]
        [Description("Acessa o Tangerino/Sólides Ponto, consulta o espelho de ponto do mês informado e retorna as batidas no formato Data | Entrada1 | Saida1 | Entrada2 | Saida2 | ... (um par Entrada/Saida por turno registrado no dia). Quando um horário não existir, retorna X.")]
        public async Task<string> ExtrairPontoAsync(
            [Description("Mês de referência desejado. Ex: agosto, 08/2026 ou Agosto/2026.")] string mesReferencia)
            => await ExtrairPontoAsync(mesReferencia, string.Empty);

        public async Task<string> ExtrairPontoAsync(
            string mesReferencia,
            string diasSelecionados)
        {
            if (string.IsNullOrWhiteSpace(_options.CodigoEmpregador) || string.IsNullOrWhiteSpace(_options.Pin))
                return "[FALHA_TECNICA] Credenciais do Tangerino não configuradas.";

            if (string.IsNullOrWhiteSpace(mesReferencia))
                return "[FALHA_TECNICA] Mês de referência não informado para consulta do Tangerino.";

            try
            {
                await InicializarNavegadorAsync();
                await GarantirAutenticacaoAsync();
                await NavegarParaEspelhoPontoAsync(mesReferencia);

                var dadosExtraidos = await ExtrairDadosPontoPorInputsAsync(diasSelecionados, mesReferencia);
                if (string.IsNullOrWhiteSpace(dadosExtraidos))
                    return "[FALHA_NEGOCIO] Nenhuma batida de ponto foi encontrada no Tangerino para o período informado.";

                var builder = new StringBuilder();
                builder.AppendLine("Data | Horário Entrada 1 | Horário Saída 1 | Horário Entrada 2 | Horário Saída 2 | ... (um par por turno)");
                builder.Append(dadosExtraidos.Trim());

                var relatorioFormatado = builder.ToString().Trim();
                _logger.LogInformation("Dados extraídos do Tangerino: \n{dados}", relatorioFormatado);

                return relatorioFormatado;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[Tangerino] Falha ao extrair espelho de ponto.");
                return $"[FALHA_TECNICA] Falha ao extrair espelho de ponto do Tangerino: {ex.Message}";
            }
        }

        private async Task InicializarNavegadorAsync()
        {
            if (_navegadorIniciado && _page != null)
                return;

            _logger.LogInformation("[Tangerino] Inicializando Playwright para consulta de ponto.");
            var playwright = await Playwright.CreateAsync();
            _browser = await playwright.Chromium.LaunchAsync(new BrowserTypeLaunchOptions
            {
                Headless = false,
                SlowMo = 250
            });

            var context = await _browser.NewContextAsync();
            context.SetDefaultTimeout(30000);
            context.SetDefaultNavigationTimeout(30000);

            _page = await context.NewPageAsync();
            _page.SetDefaultTimeout(30000);
            _page.SetDefaultNavigationTimeout(30000);
            _navegadorIniciado = true;
        }

        private async Task GarantirAutenticacaoAsync()
        {
            if (_page == null)
                throw ProcessamentoAgenteException.FalhaPlaywright("Página do Tangerino não foi inicializada.");

            var timeout = Math.Max(_options.TimeoutSegundos, 10) * 1000;

            _logger.LogInformation("[Tangerino] Navegando para login funcionário. Url={Url}", _options.UrlLoginFuncionario);
            await _page.GotoAsync(_options.UrlLoginFuncionario, new PageGotoOptions
            {
                WaitUntil = WaitUntilState.NetworkIdle,
                Timeout = timeout
            });

            await LogarDiagnosticoPaginaAsync("após navegação direta para login funcionário");

            if (await PaginaSessaoExpiradaAsync() || !await FormularioLoginFuncionarioVisivelAsync())
            {
                _logger.LogInformation("[Tangerino] Formulário de colaborador ainda não visível. Tentando ativar aba/link Colaborador.");
                await AcessarLoginColaboradorAsync(timeout);
            }

            if (!await FormularioLoginFuncionarioVisivelAsync())
                throw ProcessamentoAgenteException.FalhaPlaywright("Formulário de Login Funcionário do Tangerino não foi encontrado.");

            await PreencherCampoDiretoAsync(
                valor: _options.CodigoEmpregador,
                selector: "input[name='codigoEmpregador']",
                nomeCampo: "Código do Empregador");

            await PreencherCampoDiretoAsync(
                valor: _options.Pin,
                selector: "input[name='pin']",
                nomeCampo: "PIN");

            var botaoEntrar = _page.Locator("input[name='btnLogin'], input[value*='Acessar como Colaborador'], button:has-text('Acessar como Colaborador'), button:has-text('Entrar'), button:has-text('Login'), input[type='submit'], button[type='submit']").First;
            if (await botaoEntrar.CountAsync() == 0)
                throw ProcessamentoAgenteException.FalhaPlaywright("Botão de login do Tangerino não foi encontrado.");

            _logger.LogInformation("[Tangerino] Clicando no botão de login.");
            await botaoEntrar.ClickAsync(new LocatorClickOptions { Timeout = timeout });
            try
            {
                await _page.WaitForLoadStateAsync(LoadState.DOMContentLoaded, new PageWaitForLoadStateOptions { Timeout = timeout });
                await _page.WaitForTimeoutAsync(2000);
            }
            catch (TimeoutException ex)
            {
                _logger.LogWarning(ex, "[Tangerino] Timeout aguardando carregamento após login. Continuando para URL direta.");
            }

            _logger.LogInformation("[Tangerino] Login concluído.");
        }

        private async Task NavegarParaEspelhoPontoAsync(string mesReferencia)
        {
            if (_page == null)
                throw ProcessamentoAgenteException.FalhaPlaywright("Página do Tangerino não foi inicializada.");

            _logger.LogInformation("[Tangerino] Acessando URL direta de apropriação de horas. Url={Url}", _options.UrlEspelhoPonto);
            try
            {
                await _page.GotoAsync(_options.UrlEspelhoPonto, new PageGotoOptions
                {
                    WaitUntil = WaitUntilState.DOMContentLoaded,
                    Timeout = Math.Max(_options.TimeoutSegundos, 10) * 1000
                });
            }
            catch (PlaywrightException ex) when (ex.Message.Contains("ERR_ABORTED", StringComparison.OrdinalIgnoreCase))
            {
                _logger.LogWarning(ex, "[Tangerino] Navegação direta retornou ERR_ABORTED. Continuando e aguardando estabilização da página atual.");
            }

            try
            {
                await _page.WaitForLoadStateAsync(LoadState.DOMContentLoaded, new PageWaitForLoadStateOptions { Timeout = 10000 });
            }
            catch (TimeoutException ex)
            {
                _logger.LogWarning(ex, "[Tangerino] Timeout aguardando DOMContentLoaded após URL direta. Continuando para diagnóstico.");
            }

            await LogarDiagnosticoPaginaAsync("após navegação direta para Apropriação de horas");

            var campoMes = _page.Locator("input[placeholder*='Mês'], input[placeholder*='Mes'], input[name*='mes'], input[id*='mes'], input[type='month']").First;
            if (await campoMes.CountAsync() > 0)
            {
                _logger.LogInformation("[Tangerino] Preenchendo filtro de mês: {MesReferencia}", mesReferencia);
                await campoMes.FillAsync(mesReferencia);
            }

            var botaoConsultar = _page.Locator("button:has-text('Consultar'), input[value*='Consultar'], button:has-text('Buscar'), input[value*='Buscar'], button[type='submit']").First;
            if (await botaoConsultar.CountAsync() > 0)
            {
                var bodyText = await _page.Locator("body").InnerTextAsync(new LocatorInnerTextOptions { Timeout = 5000 });
                if (Regex.IsMatch(bodyText, @"\b\d{2}/\d{2}/\d{4}\b"))
                {
                    _logger.LogInformation("[Tangerino] Página já contém registros de ponto. Pulando clique em Consultar.");
                }
                else
                {
                    _logger.LogInformation("[Tangerino] Clicando para consultar ponto.");
                    await botaoConsultar.ClickAsync();
                    try
                    {
                        await _page.WaitForLoadStateAsync(LoadState.DOMContentLoaded, new PageWaitForLoadStateOptions { Timeout = 10000 });
                    }
                    catch (TimeoutException ex)
                    {
                        _logger.LogWarning(ex, "[Tangerino] Timeout após consultar ponto. Continuando para extração do DOM atual.");
                    }
                }
            }
        }

        private async Task<bool> NavegarPeloMenuApropriacaoAsync()
        {
            if (_page == null)
                return false;

            var clicouPonto = await ClicarPrimeiroDisponivelAsync(
                cssSelectors: new[]
                {
                    "span.nome-menu.menu6:has-text('Ponto')",
                    "span.oculta-contraido.nome-menu.menu6",
                    "span.nome-menu:has-text('Ponto')"
                },
                textos: new[] { "Ponto" },
                descricao: "menu Ponto");

            if (!clicouPonto)
                return false;

            var clicouColaborador = await ClicarPrimeiroDisponivelAsync(
                cssSelectors: new[]
                {
                    "span.nome-menu-nivel-2.elemento-menu-nome-19:has-text('Colaborador')",
                    "span.nome-menu-nivel-2:has-text('Colaborador')"
                },
                textos: new[] { "Colaborador" },
                descricao: "submenu Colaborador");

            if (!clicouColaborador)
                return false;

            var clicouApropriacao = await ClicarPrimeiroDisponivelAsync(
                cssSelectors: new[]
                {
                    "span.elemento-menu-nome-68:has-text('Apropriação de horas')",
                    "span:has-text('Apropriação de horas')",
                    "a[href*='apropriacao-horas']"
                },
                textos: new[] { "Apropriação de horas", "Apropriacao de horas" },
                descricao: "item Apropriação de horas");

            if (!clicouApropriacao)
                return false;

            try
            {
                await _page.WaitForLoadStateAsync(LoadState.NetworkIdle, new PageWaitForLoadStateOptions { Timeout = Math.Max(_options.TimeoutSegundos, 10) * 1000 });
            }
            catch (TimeoutException ex)
            {
                _logger.LogWarning(ex, "[Tangerino] Timeout aguardando NetworkIdle após abrir Apropriação de horas. Continuando.");
            }

            return true;
        }

        private async Task<bool> ClicarPrimeiroDisponivelAsync(IEnumerable<string> cssSelectors, IEnumerable<string> textos, string descricao)
        {
            if (_page == null)
                return false;

            foreach (var selector in cssSelectors)
            {
                var locator = _page.Locator(selector).First;
                if (await locator.CountAsync() > 0)
                {
                    _logger.LogInformation("[Tangerino] Clicando em {Descricao} por seletor {Selector}.", descricao, selector);
                    await locator.ClickAsync(new LocatorClickOptions { Timeout = 10000 });
                    return true;
                }
            }

            foreach (var texto in textos)
            {
                var locator = _page.GetByText(texto, new PageGetByTextOptions { Exact = true }).First;
                if (await locator.CountAsync() > 0)
                {
                    _logger.LogInformation("[Tangerino] Clicando em {Descricao} por texto {Texto}.", descricao, texto);
                    await locator.ClickAsync(new LocatorClickOptions { Timeout = 10000 });
                    return true;
                }
            }

            _logger.LogWarning("[Tangerino] Não foi possível localizar {Descricao}.", descricao);
            return false;
        }

        private async Task<string> ExtrairDadosPontoPorInputsAsync(string diasSelecionados, string mesReferencia)
        {
            if (_page == null)
                throw ProcessamentoAgenteException.FalhaPlaywright("Página do Tangerino não foi inicializada.");

            _logger.LogInformation("[Tangerino] Tentando extrair dados por table.tt/input.entrada/input.saida.");
            var temTabela = await _page.Locator("table.tt").CountAsync() > 0;

            if (!temTabela)
            {
                _logger.LogInformation("[Tangerino] table.tt não encontrada. Usando fallback por texto do body.");
                var bodyText = await _page.Locator("body").InnerTextAsync(new LocatorInnerTextOptions { Timeout = 5000 });
                _logger.LogInformation("[Tangerino] Body bruto capturado para fallback:\n{BodyText}", bodyText);
                var dadosPorTexto = ExtrairDadosPontoDoTexto(bodyText);
                var dadosPorTextoFiltrados = FiltrarDiasSelecionados(dadosPorTexto, diasSelecionados, mesReferencia);
                _logger.LogInformation("Dados capturados do Tangerino via fallback texto após filtro de dias ({Dias}):\n{dados}", diasSelecionados, dadosPorTextoFiltrados);
                return dadosPorTextoFiltrados.Trim();
            }

            var dadosExtraidos = await _page.EvaluateAsync<string>(@"() => {
    const pontosPorData = new Map();
    const linhas = document.querySelectorAll('table.tt tbody tr');
    let dataAtual = '';

    linhas.forEach(tr => {
        const dataTd = tr.querySelector('td:nth-child(2) span:nth-child(2)');
        if (dataTd && dataTd.innerText.trim() !== '') {
            dataAtual = dataTd.innerText.split(' - ')[0].trim();
        }

        if (!dataAtual) {
            return;
        }

        const entradas = Array.from(tr.querySelectorAll('input.entrada'))
            .map(input => input.value.trim() || 'X');
        const saidas = Array.from(tr.querySelectorAll('input.saida'))
            .map(input => input.value.trim() || 'X');

        if (!pontosPorData.has(dataAtual)) {
            pontosPorData.set(dataAtual, { entradas: [], saidas: [] });
        }

        const pontos = pontosPorData.get(dataAtual);
        pontos.entradas.push(...entradas);
        pontos.saidas.push(...saidas);
    });

    let relatorio = '';
    pontosPorData.forEach((pontos, data) => {
        const totalTurnos = Math.max(pontos.entradas.length, pontos.saidas.length);
        const pares = [];
        for (let i = 0; i < totalTurnos; i++) {
            pares.push(pontos.entradas[i] || 'X', pontos.saidas[i] || 'X');
        }
        relatorio += data + ' | ' + pares.join(' | ') + '\n';
    });

    return relatorio;
}");

            _logger.LogInformation("Dados capturados do Tangerino:\n{dados}", dadosExtraidos);
            _logger.LogInformation("Relatório completo do Tangerino encaminhado ao orquestrador. Dias solicitados={Dias}", diasSelecionados);
            return dadosExtraidos.Trim();
        }

        private static string ExtrairDadosPontoDoTexto(string texto)
        {
            var linhas = new List<string>();
            var matches = Regex.Matches(texto, @"\b\d{2}/\d{2}/\d{4}\b\s*-\s*[^\r\n]+");

            for (var index = 0; index < matches.Count; index++)
            {
                var dataTexto = Regex.Match(matches[index].Value, @"\b\d{2}/\d{2}/\d{4}\b").Value;
                if (string.IsNullOrWhiteSpace(dataTexto))
                    continue;

                var inicio = matches[index].Index;
                var fim = index + 1 < matches.Count ? matches[index + 1].Index : texto.Length;
                var trechoDia = texto.Substring(inicio, fim - inicio);

                var entradas = Regex.Matches(trechoDia, @"Entrada:\s*\d{2}/\d{2}/\d{4}\s+(\d{1,2}:\d{2})")
                    .Select(match => NormalizarHorario(match.Groups[1].Value))
                    .ToList();

                var saidas = Regex.Matches(trechoDia, @"Sa[ií]da:\s*\d{2}/\d{2}/\d{4}\s+(\d{1,2}:\d{2})")
                    .Select(match => NormalizarHorario(match.Groups[1].Value))
                    .ToList();

                if (entradas.Count == 0 && saidas.Count == 0)
                    continue;

                var totalTurnos = Math.Max(entradas.Count, saidas.Count);
                var pares = new List<string>();
                for (var turno = 0; turno < totalTurnos; turno++)
                {
                    pares.Add(entradas.ElementAtOrDefault(turno) ?? "X");
                    pares.Add(saidas.ElementAtOrDefault(turno) ?? "X");
                }

                linhas.Add($"{dataTexto} | {string.Join(" | ", pares)}");
            }

            return string.Join(Environment.NewLine, linhas);
        }

        private static string FiltrarDiasSelecionados(string dadosExtraidos, string diasSelecionados, string mesReferencia)
        {
            dadosExtraidos = dadosExtraidos.Replace("\\n", Environment.NewLine);
            var dias = NormalizarDiasSelecionados(diasSelecionados, mesReferencia);
            if (dias.Count == 0)
                return dadosExtraidos;

            var linhas = dadosExtraidos
                .Split(new[] { "\r\n", "\n", "\r" }, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Where(linha =>
                {
                    var dataTexto = linha.Split('|')[0].Trim().Replace('-', '/');
                    return DateTime.TryParse(dataTexto, new CultureInfo("pt-BR"), DateTimeStyles.None, out var data) && dias.Contains(data.Date);
                });

            return string.Join(Environment.NewLine, linhas);
        }

        private static HashSet<DateTime> NormalizarDiasSelecionados(string diasSelecionados, string mesReferencia)
        {
            var resultado = new HashSet<DateTime>();
            var (mesReferenciaNumero, anoReferencia) = ExtrairMesAnoReferencia(mesReferencia);

            var tokens = diasSelecionados
                .Split(new[] { ',', ';', '\n', '\r', '|'}, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

            foreach (var token in tokens)
            {
                var texto = token.Replace('-', '/');
                if (int.TryParse(texto, out var diaAvulso) && mesReferenciaNumero.HasValue && diaAvulso is >= 1 and <= 31)
                {
                    resultado.Add(new DateTime(anoReferencia, mesReferenciaNumero.Value, diaAvulso));
                    continue;
                }

                if (DateTime.TryParse(texto, new CultureInfo("pt-BR"), DateTimeStyles.None, out var dataCompleta))
                {
                    resultado.Add(dataCompleta.Date);
                    continue;
                }

                if (DateTime.TryParse($"{texto}/{anoReferencia}", new CultureInfo("pt-BR"), DateTimeStyles.None, out var dataSemAno))
                    resultado.Add(dataSemAno.Date);
            }

            return resultado;
        }

        private static (int? Mes, int Ano) ExtrairMesAnoReferencia(string mesReferencia)
        {
            var anoReferencia = DateTime.Now.Year;
            var matchAno = Regex.Match(mesReferencia, @"\b(20\d{2})\b");
            if (matchAno.Success && int.TryParse(matchAno.Value, out var anoExtraido))
                anoReferencia = anoExtraido;

            var texto = RemoverAcentos(mesReferencia).ToLowerInvariant();
            var meses = new Dictionary<string, int>
            {
                ["janeiro"] = 1,
                ["fevereiro"] = 2,
                ["marco"] = 3,
                ["abril"] = 4,
                ["maio"] = 5,
                ["junho"] = 6,
                ["julho"] = 7,
                ["agosto"] = 8,
                ["setembro"] = 9,
                ["outubro"] = 10,
                ["novembro"] = 11,
                ["dezembro"] = 12
            };

            foreach (var mes in meses)
            {
                if (texto.Contains(mes.Key, StringComparison.OrdinalIgnoreCase))
                    return (mes.Value, anoReferencia);
            }

            var matchMesNumerico = Regex.Match(mesReferencia, @"\b(0?[1-9]|1[0-2])\b");
            if (matchMesNumerico.Success && int.TryParse(matchMesNumerico.Value, out var mesNumero))
                return (mesNumero, anoReferencia);

            return (null, anoReferencia);
        }

        private static string RemoverAcentos(string texto)
        {
            var normalizado = texto.Normalize(NormalizationForm.FormD);
            var builder = new StringBuilder();

            foreach (var caractere in normalizado)
            {
                if (CharUnicodeInfo.GetUnicodeCategory(caractere) != UnicodeCategory.NonSpacingMark)
                    builder.Append(caractere);
            }

            return builder.ToString().Normalize(NormalizationForm.FormC);
        }

        private static IEnumerable<string> QuebrarTextoPorData(string texto)
        {
            var matches = Regex.Matches(texto, @"\b\d{2}[/-]\d{2}[/-]\d{4}\b");
            if (matches.Count == 0)
                yield break;

            for (int index = 0; index < matches.Count; index++)
            {
                var inicio = matches[index].Index;
                var fim = index + 1 < matches.Count ? matches[index + 1].Index : texto.Length;
                yield return texto.Substring(inicio, fim - inicio);
            }
        }

        private static string? FormatarLinhaPonto(string texto)
        {
            if (string.IsNullOrWhiteSpace(texto))
                return null;

            var dataMatch = Regex.Match(texto, @"\b\d{2}[/-]\d{2}[/-]\d{4}\b");
            if (!dataMatch.Success)
                return null;

            var horarios = Regex.Matches(texto, @"\b\d{1,2}:\d{2}\b")
                .Select(match => NormalizarHorario(match.Value))
                .Where(horario => !string.IsNullOrWhiteSpace(horario))
                .Distinct()
                .ToList();

            if (horarios.Count == 0)
                return null;

            return $"{NormalizarData(dataMatch.Value)} | {string.Join(" | ", horarios)}";
        }

        private static string NormalizarData(string data)
        {
            return data.Replace('-', '/');
        }

        private static string NormalizarHorario(string horario)
        {
            var partes = horario.Split(':');
            if (partes.Length != 2)
                return horario;

            return $"{int.Parse(partes[0]):00}:{int.Parse(partes[1]):00}";
        }

        private async Task<bool> PaginaSessaoExpiradaAsync()
        {
            if (_page == null)
                return false;

            return await _page.Locator("text=Sessão expirada").CountAsync() > 0 ||
                   await _page.Locator("text=Sessao expirada").CountAsync() > 0;
        }

        private async Task<bool> FormularioLoginFuncionarioVisivelAsync()
        {
            if (_page == null)
                return false;

            return await _page.GetByLabel("Código do Empregador *").CountAsync() > 0 ||
                   await _page.GetByLabel("Código do Empregador").CountAsync() > 0 ||
                   await _page.GetByLabel("Codigo do Empregador").CountAsync() > 0 ||
                   await _page.GetByLabel("PIN *").CountAsync() > 0 ||
                   await _page.GetByLabel("PIN").CountAsync() > 0 ||
                   await _page.Locator("input[name='codigoEmpregador'], input[placeholder*='código do empregador' i], input[placeholder*='codigo do empregador' i], input[name*='empregador' i]").CountAsync() > 0 ||
                   await _page.Locator("input[name='pin'], input[placeholder*='PIN' i], input[placeholder*='seu PIN' i]").CountAsync() > 0;
        }

        private async Task AcessarLoginColaboradorAsync(float timeout)
        {
            if (_page == null)
                throw ProcessamentoAgenteException.FalhaPlaywright("Página do Tangerino não foi inicializada.");

            var linkColaborador = await ObterLinkColaboradorAsync();
            if (linkColaborador != null)
            {
                _logger.LogInformation("[Tangerino] Clicando em Colaborador/Login Funcionário.");
                await linkColaborador.ClickAsync(new LocatorClickOptions { Timeout = timeout });
                await _page.WaitForLoadStateAsync(LoadState.NetworkIdle, new PageWaitForLoadStateOptions { Timeout = timeout });
                await LogarDiagnosticoPaginaAsync("após clique em Colaborador/Login Funcionário");
            }

            if (await FormularioLoginFuncionarioVisivelAsync())
                return;

            _logger.LogInformation("[Tangerino] Aba/link Colaborador não resolveu. Navegando para a página raiz e tentando novamente.");
            await _page.GotoAsync(_options.UrlBase, new PageGotoOptions
            {
                WaitUntil = WaitUntilState.NetworkIdle,
                Timeout = timeout
            });

            await LogarDiagnosticoPaginaAsync("após navegação para raiz do Tangerino");

            linkColaborador = await ObterLinkColaboradorAsync();
            if (linkColaborador != null)
            {
                _logger.LogInformation("[Tangerino] Clicando em Colaborador/Login Funcionário pela raiz.");
                await linkColaborador.ClickAsync(new LocatorClickOptions { Timeout = timeout });
                await _page.WaitForLoadStateAsync(LoadState.NetworkIdle, new PageWaitForLoadStateOptions { Timeout = timeout });
                await LogarDiagnosticoPaginaAsync("após clique em Colaborador/Login Funcionário pela raiz");
            }
        }

        private async Task<ILocator?> ObterLinkColaboradorAsync()
        {
            if (_page == null)
                return null;

            var seletoresCss = new[]
            {
                "a[href*='loginFuncionario']",
                "a.login-aba:has-text('Colaborador')"
            };

            foreach (var seletor in seletoresCss)
            {
                var locator = _page.Locator(seletor).First;
                if (await locator.CountAsync() > 0)
                    return locator;
            }

            var textos = new[] { "Colaborador", "Login Funcionário", "Login Funcionario" };
            foreach (var texto in textos)
            {
                var locator = _page.GetByText(texto, new PageGetByTextOptions { Exact = true }).First;
                if (await locator.CountAsync() > 0)
                    return locator;
            }

            return null;
        }

        private async Task PreencherCampoDiretoAsync(string valor, string selector, string nomeCampo)
        {
            if (_page == null)
                throw ProcessamentoAgenteException.FalhaPlaywright("Página do Tangerino não foi inicializada.");

            var campo = _page.Locator(selector).First;
            if (await campo.CountAsync() == 0)
                throw ProcessamentoAgenteException.FalhaPlaywright($"Campo '{nomeCampo}' não foi encontrado no login do Tangerino.");

            _logger.LogInformation("[Tangerino] Preenchendo campo {Campo} por seletor {Selector}.", nomeCampo, selector);
            await campo.FillAsync(valor, new LocatorFillOptions { Timeout = 10000 });
        }

        private async Task LogarDiagnosticoPaginaAsync(string contexto)
        {
            if (_page == null)
                return;

            try
            {
                var titulo = await _page.TitleAsync();
                var url = _page.Url;
                var body = await _page.Locator("body").InnerTextAsync(new LocatorInnerTextOptions { Timeout = 3000 });
                var resumo = body.Length > 1200 ? body.Substring(0, 1200) : body;
                _logger.LogInformation("[Tangerino][Diagnóstico] Contexto={Contexto}; Título={Titulo}; Url={Url}; BodyResumo=\n{BodyResumo}", contexto, titulo, url, resumo);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "[Tangerino][Diagnóstico] Falha ao coletar diagnóstico da página. Contexto={Contexto}", contexto);
            }
        }
    }
}
