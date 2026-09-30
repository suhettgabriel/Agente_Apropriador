# ESTRUTURA COMPLETA - PASSOS 3 E 4

## Árvore de Arquivos Criados

```
Apropriacao.Infrastructure/
├── Services/
│   ├── PdfReaderService.cs ✅
│   │   └── Implementação com UglyToad.PdfPig
│   │   └── Métodos: ExtrairTextoAsync, ExtrairTextoPagemAsync, ObterNumeroPaginasAsync
│   │
│   ├── ExcelReaderService.cs ✅
│   │   └── Implementação com ClosedXML
│   │   └── Métodos: ExtrairAtividadesAsync, ExtrairAtividadesPorMesAsync, ObterResumoAsync, ObterAtividadePorDataETurnoAsync
│   │
│   └── GerenciadorArquivoTemporario.cs ✅
│       └── Gerencia arquivos temporários
│       └── Métodos: SalvarArquivoTemporarioAsync, RemoverArquivoTemporarioAsync, RemoverArquivosTemporarioAsync, ObterPastaTemporaria
│
└── Plugins/
    └── PortalApropriacaoPlugin.cs ✅
        └── [KernelFunction] anotação
        └── Implementação de IAutomacaoPortalPlugin
        └── Lógica completa de Playwright
        └── Método: LancarHorasAsync()

Apropriacao.Application/
├── DTOs/
│   ├── ProcessarApropriacaoRequest.cs ✅
│   │   └── MapProcessoRequest com campos de arquivo
│   │
│   └── ProcessarApropriacaoResponse.cs ✅
│       └── Resposta com Sucesso, Mensagem, Detalhes, QuantidadeLancamentos
│
├── Commands/
│   └── ProcessarApropriacaoCommand.cs ✅
│       └── CQRS Command com Streams dos arquivos
│
└── Handlers/
    └── ProcessarApropriacaoCommandHandler.cs ✅
        └── Orquestrador principal (8 etapas)
        └── Instancia Semantic Kernel + Gemini
        └── Executa agente com Auto-Invoke
```

---

## Fluxo de Dados - Diagrama Simplificado

```
┌─────────────────────────────┐
│  Cliente HTTP               │
│  (Envia 3 dados)            │
│  • PDF                      │
│  • Excel                    │
│  • Mês Referência           │
└──────────────┬──────────────┘
               │
               ▼
┌─────────────────────────────┐
│  API Endpoint               │
│  POST /api/apropriacao/iniciar
│  (Cria Command)             │
└──────────────┬──────────────┘
               │
               ▼
┌─────────────────────────────┐
│  ProcessarApropriacaoCommand│
│  (CQRS - encapsula dados)   │
└──────────────┬──────────────┘
               │
               ▼
┌──────────────────────────────────────┐
│  ProcessarApropriacaoCommandHandler  │
│                                      │
│  Etapa 1: Validação                  │
│  Etapa 2: Salvar Arquivos            │
│  Etapa 3: Extrair PDF                │
│  ├─ PdfReaderService                 │
│  │  └─ ExtrairTextoAsync()           │
│  │                                   │
│  Etapa 4: Extrair Excel              │
│  ├─ ExcelReaderService               │
│  │  └─ ExtrairAtividadesPorMesAsync()│
│  │                                   │
│  Etapa 5: Construir System Prompt    │
│  ├─ Regras de negócio (1º/2º turno) │
│  ├─ Dados do PDF                     │
│  ├─ Atividades do Excel              │
│  │                                   │
│  Etapa 6: Kernel + Gemini            │
│  ├─ AddGoogleAIGeminiChatCompletion()│
│  ├─ AddPlugin(PortalApropriacaoPlugin│
│  │                                   │
│  Etapa 7: Executar Agente            │
│  ├─ ChatHistory com System Prompt    │
│  ├─ FunctionChoiceBehavior.Auto()    │
│  ├─ GetChatMessageContentAsync()     │
│  └─ Gemini invoca LancarHoras() N x  │
│     └─ PortalApropriacaoPlugin       │
│        └─ Playwright                 │
│           └─ Browser (headless=false)│
│              └─ UI Preenchimento     │
│                 ├─ Cliente           │
│                 ├─ Projeto           │
│                 ├─ Hora Início       │
│                 ├─ Hora Fim          │
│                 ├─ Descrição         │
│                 └─ Registrar         │
│                                      │
│  Etapa 8: Limpeza                    │
│  └─ RemoverArquivosTemporario()      │
└──────────────┬───────────────────────┘
               │
               ▼
┌─────────────────────────────────┐
│  ProcessarApropriacaoResponse   │
│  • Sucesso: true/false          │
│  • Mensagem                     │
│  • Detalhes do processamento    │
│  • Qtd lancamentos              │
│  • Lista de erros               │
└──────────────┬──────────────────┘
               │
               ▼
┌─────────────────────────────┐
│  API Response (JSON)        │
│  Retorna ao Cliente         │
└─────────────────────────────┘
```

---

## Dependências de Injeção Necessárias (Passo 5)

No `Program.cs` da API, você deve registrar:

```csharp
// Domain Services
builder.Services.AddScoped<IPdfReaderService, PdfReaderService>();
builder.Services.AddScoped<IExcelReaderService, ExcelReaderService>();
builder.Services.AddScoped<IGerenciadorArquivoTemporario, GerenciadorArquivoTemporario>();

// Handler (com Gemini API Key)
string geminiApiKey = builder.Configuration["GeminiApiKey"];
builder.Services.AddScoped(sp =>
    new ProcessarApropriacaoCommandHandler(
        sp.GetRequiredService<IPdfReaderService>(),
        sp.GetRequiredService<IExcelReaderService>(),
        sp.GetRequiredService<IGerenciadorArquivoTemporario>(),
        geminiApiKey
    )
);
```

---

## Variáveis de Ambiente Necessárias

No arquivo `appsettings.json`:

```json
{
  "GeminiApiKey": "SUA_API_KEY_DO_GOOGLE_GEMINI"
}
```

Ou em variáveis de ambiente do sistema.

---

## Pontos-Chave de Implementação

### 1. System Prompt do Agente
O System Prompt é construído dinamicamente e contém:
- Identidade clara do agente
- Regras de negócio específicas (1º turno = manhã, 2º turno = tarde)
- Cliente e Projeto padrão (DB Diagnósticos, Squad Toxicológico)
- Texto completo do PDF extraído
- Todas as atividades do Excel formatadas
- Instruções para invocar a ferramenta LancarHoras

### 2. Auto-Invoke do Gemini
```csharp
FunctionChoiceBehavior = FunctionChoiceBehavior.Auto()
```
Isso permite que o Gemini decida automaticamente QUANDO e QUANTAS VEZES chamar o método `LancarHoras()`.

### 3. Playwright com Headless = false
Isso permite visualizar a automação acontecendo em tempo real no navegador.

### 4. Tratamento de Erros
- Cada etapa valida seus inputs
- Try-catch granular no Playwright
- Limpeza garantida no finally block

### 5. Gerenciamento de Memória
- Arquivos salvos com nomes únicos (timestamp + GUID)
- Removidos automaticamente após uso
- Navegador browser destruído no finalizer

---

## Próximo: Passo 5 (API Layer)

O Passo 5 implementará:
- `Program.cs` com configuração completa
- Injeção de dependências
- Endpoint POST `/api/apropriacao/iniciar`
- Tratamento de multipart/form-data
- Swagger UI para testes
- CORS configurado

Seguido do Passo 6 (Frontend Angular 18):
- Service HTTP com FormData
- Componente com Reactive Forms
- Inputs de arquivo e texto
- Estados de loading
- Exibição de mensagens de sucesso/erro
- CSS moderno e responsivo
