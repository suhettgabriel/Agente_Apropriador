# Agente Apropriador

Aplicação local para automatizar a apropriação de horas. O sistema combina as atividades de uma planilha Excel com as batidas registradas no Tangerino/Sólides Ponto e preenche os lançamentos correspondentes no portal Framework.

O repositório contém o backend em .NET 8 e o frontend em Angular 18.

## Fluxo da aplicação

1. O usuário seleciona o mês, os dias e uma planilha de atividades no frontend.
2. A API valida e lê a planilha.
3. O Playwright autentica no Tangerino e consulta as batidas do período.
4. O backend relaciona cada atividade ao turno correspondente.
5. O Playwright abre o portal Framework e preenche os lançamentos.
6. A API retorna a quantidade de lançamentos e os detalhes de eventuais falhas.

O processamento atual é determinístico e não depende de IA generativa.

## Tecnologias

| Área | Tecnologia |
| --- | --- |
| API | ASP.NET Core Minimal API / .NET 8 |
| Frontend | Angular 18 e TypeScript |
| Automação web | Microsoft Playwright |
| Leitura de Excel | ClosedXML |
| Documentação da API | Swagger / OpenAPI |

## Estrutura

```text
Agente_Apropriador/
|-- Apropriacao.Api/             # Endpoints, DI, CORS e configuração
|-- Apropriacao.Application/     # Orquestração do processamento
|-- Apropriacao.Domain/          # Entidades, contratos, opções e exceções
|-- Apropriacao.Infrastructure/  # Excel, arquivos temporários e Playwright
|-- Apropriacao.Frontend/        # Aplicação Angular
|-- README.md
`-- CHECKLIST_FINAL.md
```

## Pré-requisitos

- [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0)
- [Node.js 18.19 ou superior](https://nodejs.org/)
- Acesso ao Tangerino/Sólides Ponto como colaborador
- Acesso ao portal Framework
- PowerShell para instalar o navegador do Playwright

Não é necessário instalar o Angular CLI globalmente; os scripts usam a versão local do projeto.

## Configuração local

1. Clone o repositório:

```powershell
git clone https://github.com/suhettgabriel/Agente_Apropriador.git
cd Agente_Apropriador
```

2. Crie a configuração de desenvolvimento a partir do modelo:

```powershell
Copy-Item .\Apropriacao.Api\appsettings.Development.example.json .\Apropriacao.Api\appsettings.Development.json
```

3. Preencha no novo arquivo:

```json
{
  "TangerinoCredentials": {
    "CodigoEmpregador": "SEU_CODIGO",
    "Pin": "SEU_PIN"
  }
}
```

O modelo completo também contém URLs, timeouts, local do estado de autenticação do Framework e origens permitidas pelo CORS.

> `appsettings.Development.json` e `auth.json` são ignorados pelo Git. Nunca publique credenciais ou o estado de uma sessão autenticada.

## Instalação

Restaure e compile o backend:

```powershell
dotnet restore .\Apropriacao.Api\Apropriacao.Api.slnx
dotnet build .\Apropriacao.Api\Apropriacao.Api.slnx
```

Instale o Chromium usado pela automação:

```powershell
.\Apropriacao.Api\bin\Debug\net8.0\playwright.ps1 install chromium
```

Instale as dependências do frontend:

```powershell
npm.cmd --prefix .\Apropriacao.Frontend install
```

## Execução

Inicie a API em um terminal:

```powershell
dotnet run --project .\Apropriacao.Api\Apropriacao.Api.csproj --launch-profile http
```

Endereços locais:

- API: <http://localhost:5254>
- Swagger: <http://localhost:5254/swagger>
- Health check: <http://localhost:5254/health>

Em outro terminal, inicie o Angular:

```powershell
npm.cmd --prefix .\Apropriacao.Frontend start
```

Abra <http://localhost:4200>.

Durante o processamento, navegadores visíveis serão abertos pelo Playwright. No primeiro acesso ao Framework, conclua o login SSO manualmente dentro do prazo configurado. Após o login, a sessão é salva localmente em `auth.json` para as próximas execuções.

## Formato da planilha

A primeira aba deve ter uma linha de cabeçalho e quatro colunas nesta ordem:

| Data | Turno | Descrição | Projeto |
| --- | --- | --- | --- |
| 01/09/2026 | 1 | Implementação da funcionalidade X | Projeto A |
| 01/09/2026 | 2 | Correção e validação | Projeto A |

Regras importantes:

- A primeira linha é sempre tratada como cabeçalho.
- Linhas sem data, turno, descrição ou projeto são ignoradas.
- O turno aceita números ou textos como `primeiro`, `manhã`, `segundo` e `tarde`.
- A atividade precisa pertencer ao mês e a um dos dias selecionados no frontend.
- O arquivo deve ser `.xlsx` ou `.xls` e ter no máximo 50 MB.

## API

### `GET /health`

Retorna o estado da API e o horário UTC da consulta.

### `POST /api/apropriacao/iniciar`

Recebe `multipart/form-data`:

| Campo | Tipo | Descrição |
| --- | --- | --- |
| `arquivoPlanilha` | arquivo | Planilha `.xlsx` ou `.xls` |
| `mesReferencia` | texto | Exemplo: `setembro/2026` |
| `diasSelecionados` | texto | Datas separadas por vírgula, por exemplo `01/09/2026, 02/09/2026` |

Exemplo:

```powershell
curl.exe -X POST http://localhost:5254/api/apropriacao/iniciar `
  -F "arquivoPlanilha=@atividades.xlsx" `
  -F "mesReferencia=setembro/2026" `
  -F "diasSelecionados=01/09/2026, 02/09/2026"
```

## Regras atuais

- O cliente do lançamento é definido atualmente como `DB Diagnósticos` no backend.
- Se a saída do primeiro turno estiver ausente, o sistema usa `12:00`.
- Se a saída do segundo turno estiver ausente, o sistema usa `18:00`.
- Dias ou turnos sem horários válidos no Tangerino não geram lançamento.
- Arquivos temporários são removidos ao final do processamento.

## Build e testes

```powershell
# Backend
dotnet build .\Apropriacao.Api\Apropriacao.Api.slnx --configuration Release

# Frontend
npm.cmd --prefix .\Apropriacao.Frontend run build

# Testes Angular
npm.cmd --prefix .\Apropriacao.Frontend test
```

O repositório ainda não possui um projeto automatizado de testes para o backend.

## Solução de problemas

### Frontend não conecta à API

Confirme que a API está em `http://localhost:5254` e que `http://localhost:4200` consta em `Cors:AllowedOrigins`.

### Playwright informa que o executável não existe

Compile o backend e execute novamente:

```powershell
.\Apropriacao.Api\bin\Debug\net8.0\playwright.ps1 install chromium
```

### Tangerino não autentica

Revise `CodigoEmpregador`, `Pin` e `UrlLoginFuncionario` em `appsettings.Development.json`. A automação abre o navegador em modo visível para facilitar o diagnóstico.

### Framework solicita login novamente

Conclua o SSO no navegador aberto. Se `auth.json` estiver expirado, remova somente esse arquivo e execute o processamento novamente para gerar um novo estado de sessão.

### Nenhuma atividade é encontrada

Confira a ordem das quatro colunas, as datas da planilha, o mês escolhido e os dias selecionados. Linhas incompletas são ignoradas.

## Segurança

- Mantenha credenciais apenas em arquivos locais ignorados pelo Git ou em variáveis de ambiente.
- Não compartilhe `auth.json`; ele contém dados de uma sessão autenticada.
- Revogue imediatamente qualquer credencial publicada por engano.
- Antes de tornar um fork público, revise planilhas, PDFs e outros documentos de exemplo.

## Documentação complementar

Os arquivos `RESUMO_PASSOS_*.md`, `ESTRUTURA_PASSOS_3_4.md` e `SETUP_CLI.md` registram etapas históricas de construção e podem não representar o comportamento atual. Este README e o código-fonte são as referências operacionais do projeto.
