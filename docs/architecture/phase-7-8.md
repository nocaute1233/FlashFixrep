# Fases 7 e 8 — ferramentas de mira

## Crosshair

A página Crosshair permite editar cor hexadecimal, tamanho, espessura,
espaçamento, opacidade, ponto central e contorno. A prévia é desenhada dentro
do FlashFix. Até 50 perfis são salvos em
`%LOCALAPPDATA%\FlashFix\crosshairs.json` por substituição atômica do arquivo.
O usuário pode abrir, atualizar e excluir perfis. O formato é validado antes
de salvar. A sobreposição sobre jogos ainda não está implementada; a página
informa isso explicitamente.

## Aim Trainer

Os quatro modos iniciais rodam em uma arena dentro da janela:

- **Flick**: um alvo reaparece em outra posição após cada acerto.
- **Tracking**: um alvo se move; a precisão é a proporção de amostras de
  aproximadamente 50 ms em que o ponteiro fica sobre ele. Acertos e erros
  representam amostras, não cliques.
- **Reflex Shot**: o alvo aparece após um intervalo variável; o tempo entre
  aparecer e acertar entra na média de reação.
- **Gridshot**: três alvos aparecem ao mesmo tempo; cada acerto substitui um.

O usuário escolhe dificuldade, duração e tamanho do alvo. A dificuldade
define o tamanho inicial e a velocidade do alvo em Tracking; o tamanho pode
ser ajustado antes da sessão. O cronômetro usa `Stopwatch` e a barra mostra
tempo realmente decorrido. Uma sessão concluída grava pontuação, acertos,
erros, precisão e, quando aplicável, reação média em
`%LOCALAPPDATA%\FlashFix\trainer-results.jsonl`. Encerrar antes do tempo
descarta a sessão. O timer só roda enquanto o treino está ativo.

O treino usa o ponteiro do Windows. Ainda não há controle interno de
sensibilidade nem garantia de FPS/latência específica; esses recursos exigem
medição e implementação própria. A função Display continua pendente de uma
integração verificável com APIs de fabricantes para alterar saturação.

## Verificação

- Build da solução sem erros ou avisos.
- Sete testes passaram, incluindo salvar, reabrir e excluir perfil, precisão
  de cliques, reação média e amostragem do Tracking.
- O editor abriu no aplicativo e um perfil de teste foi salvo, listado e
  excluído. Uma sessão completa do Aim Trainer foi verificada na interface;
  um clique automatizado em um alvo aumentou o contador de acertos. O modo de
  foco deixou arena, tempo e estatísticas visíveis na janela padrão.
