# FlashFix

## Código no GitHub

Este repositório contém o código-fonte, os testes e a documentação. Não é
necessário compactar a pasta do projeto: o Git envia os arquivos versionados.
O `.gitignore` exclui `bin`, `obj`, `dist`, arquivos compactados, bancos locais e
arquivos de segredos. O executável do Gerenciador de Chaves pode ser anexado a
uma **Release** do GitHub depois de validado e assinado; não deve ser adicionado
ao histórico do Git. As credenciais da API e do administrador devem ficar em
variáveis de ambiente no servidor, nunca no repositório.

As fases 1 a 5 incluem a base visual, login e licenciamento por API, Gerenciador
de Chaves, Visão Geral, motor de otimizações reversíveis e ajustes de mouse e
teclado. A página Sistema iniciou a fase 6 com diagnóstico e recomendações.
O Flash apresenta as seções no primeiro acesso de cada conta e pode ser
chamado novamente pelo menu lateral.
O Crosshair agora tem editor de perfis com prévia local; o Aim Trainer oferece
quatro modos de treino dentro da janela e histórico de resultados.
A interface atual está em português do Brasil, com animações que podem ser
desativadas em Configurações.

## Projetos

- `FlashFix.Desktop`: aplicativo WinUI 3 para usuários.
- `FlashFix.KeyManager`: aplicativo WPF separado para administradores.
- `FlashFix.Client`: cliente HTTP e contratos compartilhados.
- `FlashFix.Api`: autenticação, licenças, auditoria e persistência.
- `FlashFix.Hardware`: leitura de hardware e métricas do Windows.
- `FlashFix.Optimization`: ajustes de entrada, backup, histórico e restauração.
- `FlashFix.Core`: navegação, perfis de mira e lógica das sessões de treino.

## Desenvolver

Requer Windows 10 build 19041 ou superior, .NET 10 SDK e acesso aos pacotes
NuGet. Para executar a API em Development, configure:

- `FLASHFIX_KEY_HASH_SECRET`: 32 bytes aleatórios em Base64; mantenha o mesmo
  valor entre reinicializações do banco de dados.
- `FLASHFIX_BOOTSTRAP_ADMIN_USERNAME` e
  `FLASHFIX_BOOTSTRAP_ADMIN_PASSWORD`: usados apenas se ainda não existir
  um administrador.
- `FLASHFIX_SQLITE_PATH`: opcional em Development; por padrão, o banco fica
  em `%LOCALAPPDATA%\FlashFix\dev\api.db`.

```powershell
dotnet build FlashFix.slnx -c Debug
dotnet run --project src/FlashFix.Api/FlashFix.Api.csproj --urls http://127.0.0.1:5031
dotnet run --project src/FlashFix.Desktop/FlashFix.Desktop.csproj
dotnet run --project src/FlashFix.KeyManager/FlashFix.KeyManager.csproj
```

O cliente só aceita HTTP em loopback. Para usar outro servidor, defina
`FLASHFIX_API_URL` com um endereço HTTPS. Fora de Development, a API exige
`FLASHFIX_POSTGRES_CONNECTION` e aplica as migrações EF Core na inicialização.
Configure um endpoint TLS válido antes de permitir clientes remotos.

O Gerenciador de Chaves para Windows x64 pode ser publicado em um EXE único com
`dotnet publish src/FlashFix.KeyManager/FlashFix.KeyManager.csproj -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -p:EnableCompressionInSingleFile=true -p:PublishTrimmed=false`.
O binário de teste atual está em `dist/FlashFix-KeyManager-win-x64.exe` e ainda
não foi assinado. No login, informe o endereço HTTPS da API e a conta
administrativa; o endereço pode vir de `FLASHFIX_API_URL`. Ele não cria chaves
sem autenticação administrativa na API.

O primeiro cadastro exige nome de usuário, senha e chave. O administrador gera
a chave no Gerenciador de Chaves e a entrega ao usuário por um canal apropriado.
A chave completa é exibida apenas na emissão.
O aplicativo principal cria uma chave criptográfica do dispositivo na primeira
ativação. A API exige uma assinatura dessa chave no cadastro, no login e na
renovação da sessão. Copiar o nome de usuário, a senha e o arquivo de
identificação da instalação para outro PC não permite entrar. Quando disponível,
a chave privada fica no TPM; caso contrário, o Windows usa uma chave privada
não exportável no armazenamento local. Para trocar de PC, o administrador deve
verificar a solicitação e usar **Redefinir dispositivo** no Gerenciador de Chaves.
Contas ativadas antes dessa mudança também precisam dessa redefinição antes do
próximo login. A redefinição invalida as sessões anteriores.

O login administrativo do Gerenciador de Chaves usa a conta criada pelas
variáveis de bootstrap na primeira execução da API. Não há credenciais padrão
incluídas no EXE. O login do aplicativo principal é criado pelo usuário ao
ativar uma chave emitida pelo administrador.

Consulte [Fase 2](docs/architecture/phase-2.md),
[Fase 3](docs/architecture/phase-3.md) e
[Fases 4 a 6](docs/architecture/phase-4-6.md) para detalhes e limites atuais.
O fluxo do guia está em [Apresentação do Flash](docs/design/flash-tour.md).
Consulte também [Fases 7 e 8](docs/architecture/phase-7-8.md).
O estado da segurança e as pendências de produção estão em
[Revisão de segurança](docs/security/review-2026-10-06.md).
A [avaliação local de segurança](docs/security/assessment-2026-10-06.md)
registra os testes recentes, o achado administrativo e o preparo do Strix.

`Precisionfix v3` e `CrosshairFIx.bat` são materiais de referência local; não
são compilados nem executados pelo FlashFix.
