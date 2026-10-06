# Fase 9 — segurança e atualização (em andamento)

## Autenticação

- A API exige HTTPS em todas as requisições, exceto quando está em modo
  Development e o cliente conecta pelo loopback. Um servidor de produção
  precisa receber TLS diretamente; uma implantação atrás de proxy exigirá
  configuração explícita e confiável dos cabeçalhos encaminhados.
- O cliente não segue redirecionamentos HTTP automaticamente. Isso evita que
  uma resposta do servidor desvie uma requisição com credenciais ou token.
- A renovação de sessão compara e substitui o hash do token numa única
  operação no banco. Só uma requisição pode consumir cada token, mesmo quando
  duas tentativas chegam ao mesmo tempo.
- O login aplica o mesmo limite de tamanho e formato de senha do cadastro.
- Respostas de autenticação não revelam se a conta existe; falhas simultâneas
  incrementam o contador no banco e o bloqueio ocorre após cinco falhas.
- Sessões administrativas usam acesso de cinco minutos e renovação limitada a
  oito horas desde a criação da sessão.
- O Gerenciador de Chaves pede o endereço da API antes de receber credenciais,
  evitando um destino local implícito no EXE distribuído.

## Integridade de atualizações

`ReleaseIntegrity` valida um manifesto com versão numérica, URL HTTPS, SHA-256
do pacote e assinatura RSA-PSS/SHA-256. Depois da validação da assinatura,
confere os bytes baixados com o hash do manifesto. O conteúdo assinado é UTF-8
com quatro linhas: versão, URL, SHA-256 em hexadecimal maiúsculo e linha final
vazia. A chave privada deve permanecer fora do aplicativo e do repositório.

A distribuição automática ainda depende de um domínio de releases, chave
pública de produção fixada no cliente, processo de assinatura, certificado
para os executáveis Windows e fluxo de instalação/rollback. Esta biblioteca
não instala pacotes nem afirma que uma atualização foi publicada.

## Verificação nesta etapa

- Compilação da solução sem avisos ou erros.
- Teste de manifesto assinado, alteração indevida de campos e pacote alterado.
- API local: primeira renovação retornou 200 e repetição retornou 401; duas
  renovações simultâneas do mesmo token retornaram 200 e 401.
- Login capturado na janela de 1280×820 para inspeção visual.
