# Avaliação local de segurança — 6 de outubro de 2026

## Escopo e método

As skills `application-security-testing`, `api-security-testing` e
`find-security-vulnerabilities-in-code` orientaram a revisão do código da API,
do cliente e dos fluxos de autenticação. O teste de integração usa uma API de
desenvolvimento e um banco SQLite temporário, com credenciais aleatórias.
Nenhum serviço externo ou ambiente de produção foi testado.

O Strix 1.7.0 está instalado, mas seus agentes não foram executados. O Docker
Desktop foi instalado depois da avaliação inicial. O WSL foi atualizado para
3.0.1.0 e os recursos **Virtual Machine Platform** e **Windows Subsystem for
Linux** estão habilitados. Ainda assim, o engine do Docker não inicia: a
importação da distribuição interna `docker-desktop` falha com
`Wsl/Service/RegisterDistro/CreateVm/HCS/ERROR_NOT_FOUND`. Um reinício do WSL
e do Docker não resolveu. Portanto, esta
avaliação não é um relatório Strix nem uma comprovação de ausência de falhas.

## Achado reproduzido e corrigido

O teste inicial mostrou que uma senha administrativa válida permitia entrar de
outro dispositivo. A API agora exige um desafio assinado também para
administradores. O primeiro login bem-sucedido vincula a conta à chave pública
do dispositivo; os acessos e as renovações seguintes exigem a mesma chave.
`tests/DeviceBindingSmoke.ps1` passou a exigir rejeição de outra chave e de
login administrativo sem prova. O primeiro vínculo ainda depende de proteger
a senha de bootstrap até que a conta seja ativada; não substitui um segundo
fator independente.

## Verificações que passaram

- Cadastro e login de usuário comum no dispositivo ativado.
- Rejeição da mesma conta com outra chave privada, mesmo copiando o identificador
  de instalação.
- Rejeição de assinatura malformada e de desafio já utilizado.
- Rejeição de chamada administrativa feita por usuário comum.
- Renovação de sessão somente com prova da chave vinculada.
- Redefinição administrativa do dispositivo, revogação da sessão anterior e
  entrada com a nova chave.
- O `dotnet list FlashFix.slnx package --vulnerable --include-transitive` não
  identificou pacotes com avisos de vulnerabilidade nas fontes NuGet atuais.

## Preparação do Strix local

O script `tests/RunStrixLocal.ps1` cria uma cópia temporária apenas dos fontes
selecionados, executa o Strix nessa cópia com limite de US$ 5 por padrão e a
remove ao terminar. Ele não contém nem grava a chave da API. O identificador
de modelo configurado é `gemini/gemini-3.8-flash`; a sintaxe enviada na
conversa não corresponde ao código de modelo publicado pelo Google.

1. Resolver o erro de criação da VM do WSL mostrado no Docker Desktop. Não
   apagar nem redefinir os discos do Docker sem preservar eventuais dados.
2. Iniciar o Docker Desktop e confirmar que o engine responde a `docker info`.
   Se `docker` ainda não aparecer no PATH, o script local usa o executável da
   instalação por usuário.
3. Criar uma nova chave do Google AI Studio e configurá-la na sessão do
   PowerShell como `LLM_API_KEY`, sem colocá-la em arquivo do projeto ou em
   comandos compartilhados. Revogar a chave que foi colada na conversa.
4. Executar `powershell -NoProfile -File tests/RunStrixLocal.ps1` na raiz do projeto.
   Examinar `strix_runs/<execução>/penetration_test_report.md`, os arquivos em
   `vulnerabilities/` e `run.json` antes de aceitar qualquer resultado como
   conclusivo.

O script não foi executado até o fim porque Docker ainda não está disponível.
Nenhum gasto com o provedor do modelo ocorreu nesta avaliação.

### Diagnóstico do Docker em 6 de outubro

O Docker falha ao registrar `docker-desktop` com
`Wsl/Service/RegisterDistro/CreateVm/HCS/ERROR_NOT_FOUND`. O arquivo
`%LOCALAPPDATA%\Docker\wsl\main\ext4.vhdx` existe. `WslService`, `vmcompute`
e `hns` estão em execução; os recursos `VirtualMachinePlatform` e
`Microsoft-Windows-Subsystem-Linux` estão habilitados, e o Windows detecta um
hipervisor. O serviço `HvHost`, porém, está configurado como `Disabled`. A
documentação da Microsoft recomenda `Manual` e não desativá-lo. Essa diferença
é uma hipótese de falha, ainda não uma causa confirmada. A sessão atual não
tem privilégios de administrador para restaurar o serviço ou ler o log
`Microsoft-Windows-Hyper-V-Compute-Admin`.

Próxima verificação local, em um PowerShell **como administrador**:

```powershell
Set-Service -Name HvHost -StartupType Manual
Start-Service -Name HvHost
wsl --shutdown
```

Depois, reiniciar o Docker Desktop e verificar `docker info`. Se ainda houver
falha, consultar o log `Microsoft-Windows-Hyper-V-Compute-Admin` como
administrador antes de reinstalar WSL ou redefinir o Docker. Nenhum disco do
Docker foi apagado ou redefinido.

## Atualização da publicação local

O Gerenciador de Chaves recebeu uma conta administrativa no banco SQLite de
desenvolvimento deste PC. O segredo de HMAC da API é armazenado com proteção
DPAPI do usuário Windows fora do repositório. A senha aleatória foi exibida ao
proprietário uma vez e não aparece no código-fonte nem neste relatório. A API
reiniciou usando o mesmo segredo; `/health` respondeu `ok` e a conta continuou
no banco.

O executável WinUI principal foi publicado com as bibliotecas e os recursos
`.xbf`/`.pri`. A primeira publicação não incluiu esses recursos e falhou ao
iniciar; o alvo `CopyUnpackagedWinUiResources` corrige a saída, e uma segunda
execução do EXE permaneceu ativa sem novo registro de erro de inicialização.
O EXE principal e o Gerenciador de Chaves ainda estão **sem assinatura digital**.
O levantamento NuGet não identificou pacotes com avisos conhecidos nas fontes
configuradas; isso não equivale a uma auditoria completa.

A conta administrativa local foi vinculada à chave `FlashFix.Admin.v1` deste
perfil Windows. O teste de integração confirmou que uma chave diferente e uma
tentativa sem prova do dispositivo são rejeitadas, inclusive na renovação.
Ainda faltam um segundo fator independente, recuperação administrativa auditada
e testes em uma instalação limpa antes da distribuição comercial.
