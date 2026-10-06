# Fase 2 — contas e licenças

## Entregue

- API ASP.NET Core com registo, login, refresh, logout e consulta de perfil.
- Licenças de 1, 7 ou 30 dias e vitalícias. A validade começa na ativação.
- Key associada a uma conta; associação de dispositivo por identificador de
  instalação persistido no perfil local do Windows.
- Key Manager separado para emissão, pesquisa, bloqueio, desbloqueio, revogação,
  reset de dispositivo, libertação da key, gestão de contas e histórico.
- Passwords com bcrypt. Keys e identificadores de dispositivo guardados como
  HMAC; tokens guardados como hash SHA-256.
- Access token de 15 minutos, refresh token de 30 dias, rotação no refresh e
  verificação de licença/conta em cada pedido protegido.
- Limite de pedidos por IP e bloqueio temporário após cinco falhas de login.
- SQLite apenas em Development; PostgreSQL e migração EF Core em produção.

## Esquema

`Users`: credenciais e estado da conta. `LicenseKeys`: key, duração,
ativação, validade e dispositivo. `Sessions`: hashes dos tokens, validade e
revogação. `AuditEvents`: operações de autenticação e administração.

O valor completo de uma key não é guardado. O segredo
`FLASHFIX_KEY_HASH_SECRET` é obrigatório e deve ser mantido estável, fora do
repositório e fora dos executáveis. O identificador local não é prova física de
hardware e pode ser copiado; a segurança depende da validação no servidor.

## Verificação efetuada

Compilação de toda a solução sem avisos. API exercitada em loopback com emissão,
registo, consulta de licença, rotação de refresh, rejeição do token antigo e
revogação imediata. Login administrativo, listagem de keys, login de utilizador,
consulta da licença nas definições e logout testados nas interfaces.

## Limites atuais

O PostgreSQL foi preparado com migração e SQL gerado, mas não foi executado
contra uma instância local porque PostgreSQL não está instalado nesta máquina.
TLS, assinatura, distribuição e operação de produção pertencem à Fase 9.
O rate limit atual é em memória por instância da API; para múltiplas réplicas,
será necessária limitação partilhada no gateway ou datastore.
