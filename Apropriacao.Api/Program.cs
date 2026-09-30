using Apropriacao.Application.Commands;
using Apropriacao.Application.DTOs;
using Apropriacao.Application.Handlers;
using Apropriacao.Domain.Exceptions;
using Apropriacao.Domain.Interfaces;
using Apropriacao.Domain.Options;
using Apropriacao.Infrastructure.Plugins;
using Apropriacao.Infrastructure.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;

var builder = WebApplication.CreateBuilder(args);
const long TamanhoMaximoArquivo = 50 * 1024 * 1024;

builder.Services.Configure<TangerinoOptions>(
    builder.Configuration.GetSection(TangerinoOptions.SectionName));
builder.Services.Configure<FrameworkOptions>(
    builder.Configuration.GetSection(FrameworkOptions.SectionName));

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

builder.Services.AddScoped<IExcelReaderService, ExcelReaderService>();
builder.Services.AddScoped<IGerenciadorArquivoTemporario, GerenciadorArquivoTemporario>();
builder.Services.AddScoped<ExtratorPontoTangerinoPlugin>();
builder.Services.AddScoped<PortalApropriacaoPlugin>();
builder.Services.AddScoped<ProcessarApropriacaoCommandHandler>(sp =>
{
    var excel = sp.GetRequiredService<IExcelReaderService>();
    var temp = sp.GetRequiredService<IGerenciadorArquivoTemporario>();
    var extratorPonto = sp.GetRequiredService<ExtratorPontoTangerinoPlugin>();
    var portalPlugin = sp.GetRequiredService<PortalApropriacaoPlugin>();
    var handlerLogger = sp.GetRequiredService<ILogger<ProcessarApropriacaoCommandHandler>>();

    return new ProcessarApropriacaoCommandHandler(excel, temp, extratorPonto, portalPlugin, handlerLogger);
});

builder.Services.AddCors(options =>
{
    options.AddPolicy("AngularDev", policy =>
    {
        var origins = builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>() ?? ["http://localhost:4200"];

        policy
            .WithOrigins(origins)
            .AllowAnyHeader()
            .AllowAnyMethod();
    });
});

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseCors("AngularDev");

// Redirect HTTPS is skipped in Development: the local dev cert can be missing/untrusted
// and the Angular app on http://localhost:4200 would be redirected to a broken https origin.
if (!app.Environment.IsDevelopment())
{
    app.UseHttpsRedirection();
}

app.MapGet("/health", () => Results.Ok(new { status = "ok", timestamp = DateTime.UtcNow }));
app.MapGet("/", () => Results.Redirect("/swagger"));

app.MapPost("/api/apropriacao/iniciar", async ([FromForm] IniciarApropriacaoForm form, ProcessarApropriacaoCommandHandler handler, CancellationToken cancellationToken) =>
{
    var arquivoPlanilha = form.ArquivoPlanilha;
    var mesReferencia = form.MesReferencia;
    var diasSelecionados = form.DiasSelecionados;

    if (arquivoPlanilha == null || arquivoPlanilha.Length == 0)
        return Results.BadRequest(new { mensagem = "Arquivo Excel de atividades é obrigatório." });

    if (arquivoPlanilha.Length > TamanhoMaximoArquivo)
        return Results.BadRequest(new { mensagem = "A planilha deve ter no máximo 50 MB." });

    var extensaoPlanilha = Path.GetExtension(arquivoPlanilha.FileName);
    if (!extensaoPlanilha.Equals(".xlsx", StringComparison.OrdinalIgnoreCase) && !extensaoPlanilha.Equals(".xls", StringComparison.OrdinalIgnoreCase))
        return Results.BadRequest(new { mensagem = "A planilha de atividades deve ser um arquivo .xlsx ou .xls." });

    if (string.IsNullOrWhiteSpace(mesReferencia))
        return Results.BadRequest(new { mensagem = "Mês de referência é obrigatório." });

    if (string.IsNullOrWhiteSpace(diasSelecionados))
        return Results.BadRequest(new { mensagem = "Informe pelo menos um dia para lançar." });

    try
    {
        using var planilhaStream = arquivoPlanilha.OpenReadStream();

        var command = new ProcessarApropriacaoCommand(
            planilhaStream,
            arquivoPlanilha.FileName,
            mesReferencia,
            diasSelecionados,
            cancellationToken);

        var resultado = await handler.HandleAsync(command, cancellationToken);

        if (resultado.Sucesso)
            return Results.Ok(resultado);

        return Results.BadRequest(resultado);
    }
    catch (ArquivoInvalidoException ex)
    {
        return Results.BadRequest(new ProcessarApropriacaoResponse
        {
            Sucesso = false,
            Mensagem = ex.Message,
            Erros = new List<string> { ex.Message }
        });
    }
    catch (Exception ex)
    {
        return Results.Problem(title: "Erro interno", detail: ex.Message, statusCode: 500);
    }
}).Accepts<IniciarApropriacaoForm>("multipart/form-data").DisableAntiforgery();

app.Run();

// DTO dedicado para o binding multipart/form-data: Swashbuckle não gera schema quando
// IFormFile é combinado com parâmetros [FromForm] soltos no mesmo endpoint.
sealed class IniciarApropriacaoForm
{
    public IFormFile ArquivoPlanilha { get; set; } = null!;
    public string MesReferencia { get; set; } = string.Empty;
    public string DiasSelecionados { get; set; } = string.Empty;
}
