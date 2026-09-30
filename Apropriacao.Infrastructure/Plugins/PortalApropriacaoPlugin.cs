using System.ComponentModel;
using System.Globalization;
using System.IO;
using Apropriacao.Domain.Exceptions;
using Apropriacao.Domain.Interfaces;
using Apropriacao.Domain.Options;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.Playwright;

namespace Apropriacao.Infrastructure.Plugins
{
    public class PortalApropriacaoPlugin : IAutomacaoPortalPlugin
    {
        private readonly FrameworkOptions _options;
        private readonly ILogger<PortalApropriacaoPlugin> _logger;
        private IBrowser? _browser;
        private IBrowserContext? _context;
        private IPage? _page;
        private bool _navegadorIniciado;

        public PortalApropriacaoPlugin(
            IOptions<FrameworkOptions> options,
            ILogger<PortalApropriacaoPlugin> logger)
        {
            _options = options.Value ?? throw new ArgumentNullException(nameof(options));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        [Description("Registra um turno de horas no portal da Framework preenchendo cliente, projeto, data, início, fim e descrição.")]
        public async Task<string> LancarHorasAsync(
            DateTime data,
            string turno,
            string cliente,
            string projeto,
            string horaInicio,
            string horaFim,
            string descricao)
        {
            _logger.LogInformation(
                "Lançamento determinístico -> Data: {Data}, Turno: {Turno}, Inicio: {Inicio}, Fim: {Fim}, Desc: {Descricao}, Cliente: {Cliente}, Projeto: {Projeto}",
                data.ToString("dd/MM/yyyy"), turno, horaInicio, horaFim, descricao, cliente, projeto);

            try
            {
                ValidarEntrada(data, cliente, projeto, horaInicio, horaFim, descricao);
                await InicializarNavegadorAsync();
                await GarantirPaginaFrameworkAsync();
                await PreencherFormularioAsync(data, cliente, projeto, horaInicio, horaFim, descricao);
                return $"Sucesso ao lançar o turno de {horaInicio} até {horaFim} em {data:dd/MM/yyyy} para o cliente '{cliente}' no projeto '{projeto}'.";
            }
            catch (ApropriacaoNegocioException ex)
            {
                _logger.LogError(ex, "Erro de negócio ao lançar horas na Framework.");
                return $"[FALHA_NEGOCIO] {ex.Message}";
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erro técnico ao lançar horas na Framework.");
                return $"[FALHA_TECNICA] Erro ao lançar {data:dd/MM/yyyy} ({turno}): {ex.Message}";
            }
        }

        private void ValidarEntrada(DateTime data, string cliente, string projeto, string horaInicio, string horaFim, string descricao)
        {
            if (data == default)
                throw new ArgumentException("Data do lançamento inválida.");
            if (string.IsNullOrWhiteSpace(cliente))
                throw new ArgumentException("Cliente não pode ser vazio.");
            if (string.IsNullOrWhiteSpace(projeto))
                throw new ArgumentException("Projeto não pode ser vazio.");
            if (!TimeSpan.TryParseExact(horaInicio, @"hh\:mm", CultureInfo.InvariantCulture, out _))
                throw new ArgumentException($"Hora inicial inválida: {horaInicio}.");
            if (!TimeSpan.TryParseExact(horaFim, @"hh\:mm", CultureInfo.InvariantCulture, out _))
                throw new ArgumentException($"Hora final inválida: {horaFim}.");
            if (string.IsNullOrWhiteSpace(descricao))
                throw new ArgumentException("Descrição não pode ser vazia.");
        }

        private async Task InicializarNavegadorAsync()
        {
            if (_navegadorIniciado && _page != null)
                return;

            var playwright = await Playwright.CreateAsync();
            var launchOptions = new BrowserTypeLaunchOptions
            {
                Headless = false,
                SlowMo = 250
            };

            _browser = await playwright.Chromium.LaunchAsync(launchOptions);
            var contextOptions = new BrowserNewContextOptions();
            var storageStatePath = ResolverStorageStatePath();

            if (File.Exists(storageStatePath))
            {
                contextOptions.StorageStatePath = storageStatePath;
                _logger.LogInformation("[Framework] Usando StorageState do Google em {StorageStatePath}.", storageStatePath);
            }
            else
            {
                _logger.LogWarning("[Framework] StorageState não encontrado em {StorageStatePath}. Será necessário autenticar manualmente no Google.", storageStatePath);
            }

            _context = await _browser.NewContextAsync(contextOptions);
            _context.SetDefaultTimeout(30000);
            _context.SetDefaultNavigationTimeout(30000);
            _page = await _context.NewPageAsync();
            _page.SetDefaultTimeout(30000);
            _page.SetDefaultNavigationTimeout(30000);
            _navegadorIniciado = true;
        }

        private async Task GarantirPaginaFrameworkAsync()
        {
            if (_page == null)
                throw ProcessamentoAgenteException.FalhaPlaywright("Página da Framework não foi inicializada.");

            var timeout = Math.Max(_options.TimeoutSegundos, 10) * 1000;
            _logger.LogInformation("[Framework] Navegando para {Url}.", _options.UrlApropriacaoNovo);
            await _page.GotoAsync(_options.UrlApropriacaoNovo, new PageGotoOptions
            {
                WaitUntil = WaitUntilState.DOMContentLoaded,
                Timeout = timeout
            });

            try
            {
                await _page.WaitForLoadStateAsync(LoadState.NetworkIdle, new PageWaitForLoadStateOptions { Timeout = 10000 });
            }
            catch (TimeoutException ex)
            {
                _logger.LogWarning(ex, "[Framework] NetworkIdle não ocorreu; seguindo com a validação da rota.");
            }

            if (!_page.Url.Contains("app.frwkapp.com.br", StringComparison.OrdinalIgnoreCase) ||
                !_page.Url.Contains("apropriacao/novo", StringComparison.OrdinalIgnoreCase))
            {
                _logger.LogInformation("[Framework] SSO solicitado. Aguarde a conclusão do login manual no navegador. URL atual={Url}; Tempo máximo={Tempo}s", _page.Url, _options.TempoLoginManualSegundos);

                try
                {
                    await _page.WaitForURLAsync(
                        "**/apropriacao/novo**",
                        new PageWaitForURLOptions { Timeout = Math.Max(_options.TempoLoginManualSegundos, 30) * 1000, WaitUntil = WaitUntilState.DOMContentLoaded });
                }
                catch (TimeoutException)
                {
                    throw ProcessamentoAgenteException.FalhaPlaywright(
                        $"Login manual da Framework não foi concluído em {_options.TempoLoginManualSegundos} segundos. URL atual: {_page.Url}. Faça o login no navegador aberto e tente novamente.");
                }

                if (_context != null)
                {
                    try
                    {
                        await _context.StorageStateAsync(new BrowserContextStorageStateOptions { Path = ResolverStorageStatePath() });
                        _logger.LogInformation("[Framework] StorageState salvo após login manual.");
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "[Framework] Não foi possível salvar o StorageState após o login.");
                    }
                }
            }

            _logger.LogInformation("[Framework] Rota de apropriação alcançada após autenticação. URL={Url}", _page.Url);

            var campoData = _page.Locator("input[name='date']").First;
            try
            {
                await campoData.WaitForAsync(new LocatorWaitForOptions
                {
                    State = WaitForSelectorState.Visible,
                    Timeout = timeout
                });
            }
            catch (TimeoutException)
            {
                throw ProcessamentoAgenteException.FalhaPlaywright(
                    "Formulário de apropriação da Framework não foi encontrado após o login.");
            }
        }

        private async Task PreencherFormularioAsync(DateTime data, string cliente, string projeto, string horaInicio, string horaFim, string descricao)
        {
            if (_page == null)
                throw ProcessamentoAgenteException.FalhaPlaywright("Página da Framework não foi inicializada.");

            _logger.LogInformation("[Framework] Preenchendo data {Data}.", data.ToString("yyyy-MM-dd"));
            await _page.FillAsync("input[name='date']", data.ToString("yyyy-MM-dd"));
            await _page.FillAsync("input[name='startTime']", horaInicio);
            await _page.FillAsync("input[name='endTime']", horaFim);
            await _page.FillAsync("textarea[name='activityDescription']", descricao);

            await SelecionarComboboxAsync(cliente, "Cliente");
            await SelecionarComboboxAsync(projeto, "Projeto");

            _logger.LogInformation("[Framework] Clicando em Registrar.");
            var botaoRegistrar = _page.Locator("button[type='submit']").First;
            if (await botaoRegistrar.CountAsync() == 0)
                throw ProcessamentoAgenteException.FalhaPlaywright("Botão Registrar não encontrado na Framework.");

            await botaoRegistrar.ClickAsync();
            await AguardarResultadoAsync();
        }

        private async Task SelecionarComboboxAsync(string valor, string nome)
        {
            if (_page == null)
                return;

            var comboboxes = _page.GetByRole(AriaRole.Combobox);
            var quantidadeComboboxes = await comboboxes.CountAsync();
            var indice = nome.Equals("Projeto", StringComparison.OrdinalIgnoreCase) ? 1 : 0;

            if (quantidadeComboboxes <= indice)
                throw ApropriacaoNegocioException.ValidacaoPortal($"Combobox de {nome} não encontrado na Framework.");

            var combobox = comboboxes.Nth(indice);

            if (await combobox.CountAsync() == 0)
            {
                throw ApropriacaoNegocioException.ValidacaoPortal($"Combobox de {nome} não encontrado na Framework.");
            }

            var textoAtual = (await combobox.InnerTextAsync()).Trim();
            _logger.LogInformation("[Framework] Combobox {Nome} encontrado na posição {Indice}. Valor atual={ValorAtual}; Valor esperado={ValorEsperado}", nome, indice, textoAtual, valor);
            if (textoAtual.Equals(valor, StringComparison.OrdinalIgnoreCase))
                return;

            _logger.LogInformation("[Framework] Abrindo combobox de {Nome} para selecionar {Valor}.", nome, valor);
            await combobox.ClickAsync();
            await _page.WaitForTimeoutAsync(300);

            var opcao = _page.GetByRole(AriaRole.Option)
                .Filter(new LocatorFilterOptions { HasText = valor })
                .Last;
            if (await opcao.CountAsync() == 0)
            {
                opcao = _page.Locator("[role='option']:visible, li:visible, [data-radix-collection-item]:visible")
                    .Filter(new LocatorFilterOptions { HasText = valor })
                    .Last;
            }
            if (await opcao.CountAsync() == 0)
                opcao = _page.GetByText(valor, new PageGetByTextOptions { Exact = true }).Last;

            if (await opcao.CountAsync() == 0)
                throw ApropriacaoNegocioException.ValidacaoPortal($"Opção '{valor}' não encontrada no combobox de {nome}.");

            await opcao.ClickAsync();

            var valorSelecionado = (await combobox.InnerTextAsync()).Trim();
            if (!valorSelecionado.Contains(valor, StringComparison.OrdinalIgnoreCase))
                throw ApropriacaoNegocioException.ValidacaoPortal($"Não foi possível selecionar '{valor}' no combobox de {nome}. Valor atual: '{valorSelecionado}'.");

            _logger.LogInformation("[Framework] Combobox {Nome} selecionado com {Valor}.", nome, valor);
        }

        private async Task AguardarResultadoAsync()
        {
            if (_page == null)
                return;

            var sucesso = _page.Locator("text=sucesso, text=registrado, [role='alert']").First;
            try
            {
                await sucesso.WaitForAsync(new LocatorWaitForOptions { State = WaitForSelectorState.Visible, Timeout = 10000 });
                var mensagem = await sucesso.InnerTextAsync();
                if (mensagem.Contains("erro", StringComparison.OrdinalIgnoreCase) || mensagem.Contains("já existe", StringComparison.OrdinalIgnoreCase))
                    throw ApropriacaoNegocioException.ValidacaoPortal(mensagem);
            }
            catch (TimeoutException)
            {
                _logger.LogInformation("[Framework] Nenhum alerta visível após Registrar; lançamento enviado.");
            }
        }

        private string ResolverStorageStatePath()
        {
            return Path.IsPathRooted(_options.StorageStatePath)
                ? _options.StorageStatePath
                : Path.Combine(AppContext.BaseDirectory, _options.StorageStatePath);
        }
    }
}
