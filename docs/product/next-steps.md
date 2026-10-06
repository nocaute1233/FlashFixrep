# Próximas melhorias do FlashFix

## Antes de distribuir

1. Vincular a conta administrativa a um segundo fator e oferecer recuperação
   auditada. Hoje a senha administrativa ainda permite login de outro PC.
2. Hospedar a API com HTTPS, banco protegido, backup testado e monitoramento de
   falhas de autenticação. A API local serve apenas para desenvolvimento.
3. Assinar os executáveis e testar instalação, atualização e reversão em um PC
   limpo. O EXE WinUI exige os arquivos da pasta publicada.
4. Validar as telas em diferentes escalas de exibição, tamanhos de janela e
   Windows 10/11 com navegação por teclado e leitor de tela.

## Funcionalidades a considerar

- **Perfis de ajustes:** salvar combinações de mouse e teclado para alternar
  manualmente, sempre com prévia dos valores e restauração individual.
- **Comparação de leituras:** mostrar histórico de CPU, memória e disco com a
  data de cada medição, sem prometer ganho de FPS por números isolados.
- **Display por monitor:** listar cada tela e sua frequência antes de oferecer
  ajustes; manter resolução e taxa de atualização sob controle do Windows.
- **Verificação de integridade:** mostrar versão, assinatura e origem do EXE
  antes de instalar atualizações.
- **Suporte integrado:** enviar ao usuário um resumo local de diagnóstico que
  ele possa revisar antes de compartilhar no Discord.

As novas opções devem mostrar o estado atual, explicar o efeito real e ter
restauração testada antes de aparecer como ajuste aplicável.
