# Script de Setup - Agente de Apropriação de Horas

## Passo 1: Criar a Solution e Projetos Backend

Abra um terminal PowerShell ou Command Prompt na pasta do projeto e execute os comandos abaixo sequencialmente:

```powershell
# Navegar para a pasta do projeto
cd C:\Users\gabrielsuhett_frwk\source\repos\Agente_Apropriador

# Criar a Solution
dotnet new sln -n AgenteApropriacao

# Criar os 4 projetos do backend
dotnet new classlib -n Apropriacao.Domain -f net8.0
dotnet new classlib -n Apropriacao.Application -f net8.0
dotnet new classlib -n Apropriacao.Infrastructure -f net8.0
dotnet new web -n Apropriacao.Api -f net8.0

# Adicionar os projetos à solution
dotnet sln AgenteApropriacao.sln add Apropriacao.Domain\Apropriacao.Domain.csproj
dotnet sln AgenteApropriacao.sln add Apropriacao.Application\Apropriacao.Application.csproj
dotnet sln AgenteApropriacao.sln add Apropriacao.Infrastructure\Apropriacao.Infrastructure.csproj
dotnet sln AgenteApropriacao.sln add Apropriacao.Api\Apropriacao.Api.csproj

# Configurar referências entre projetos (dependência unidirecional)
cd Apropriacao.Application
dotnet add reference ..\Apropriacao.Domain\Apropriacao.Domain.csproj
cd ..\Apropriacao.Infrastructure
dotnet add reference ..\Apropriacao.Domain\Apropriacao.Domain.csproj
cd ..\Apropriacao.Api
dotnet add reference ..\Apropriacao.Application\Apropriacao.Application.csproj
dotnet add reference ..\Apropriacao.Infrastructure\Apropriacao.Infrastructure.csproj
dotnet add reference ..\Apropriacao.Domain\Apropriacao.Domain.csproj
cd ..

# Instalar pacotes no projeto Apropriacao.Infrastructure
cd Apropriacao.Infrastructure
dotnet add package Microsoft.SemanticKernel --version 1.25.0
dotnet add package Microsoft.SemanticKernel.Connectors.Google --version 1.25.0
dotnet add package Microsoft.Playwright --version 1.48.2
dotnet add package UglyToad.PdfPig --version 0.1.8
dotnet add package ClosedXML --version 0.102.1
cd ..

# Instalar pacotes no projeto Apropriacao.Application
cd Apropriacao.Application
dotnet add package Microsoft.SemanticKernel --version 1.25.0
cd ..

# Instalar pacotes no projeto Apropriacao.Api
cd Apropriacao.Api
dotnet add package Swashbuckle.AspNetCore --version 6.8.1
dotnet add package Microsoft.SemanticKernel --version 1.25.0
cd ..

# Restaurar todas as dependências
cd ..
dotnet restore

# Instalar os navegadores do Playwright (necessário após a instalação do pacote)
pwsh Apropriacao.Infrastructure\bin\Debug\net8.0\playwright.ps1 install
```

## Passo 2: Criar o Projeto Frontend Angular

Abra um novo terminal (PowerShell ou CMD) e execute:

```powershell
# Navegar para a pasta do projeto
cd C:\Users\gabrielsuhett_frwk\source\repos\Agente_Apropriador

# Criar a aplicação Angular 18 (standalone)
ng new Apropriacao.Frontend --routing --style=css --skip-git=true

# Navegar para a pasta do frontend
cd Apropriacao.Frontend

# Instalar as dependências
npm install
```

## Verificação

Após completar os comandos, você terá a seguinte estrutura:

```
Agente_Apropriador/
├── AgenteApropriacao.sln
├── Apropriacao.Domain/
├── Apropriacao.Application/
├── Apropriacao.Infrastructure/
├── Apropriacao.Api/
└── Apropriacao.Frontend/
```

Agora está tudo pronto para implementar o Domain Layer e os demais passos.
