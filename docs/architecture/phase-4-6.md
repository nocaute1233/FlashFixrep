# Fases 4 e 5; início da fase 6

## Motor de otimizações

`FlashFix.Optimization` concentra leitura, aplicação e restauração. Para cada
ajuste, o motor grava um backup JSON do valor original em
`%LOCALAPPDATA%\FlashFix\optimizations` **antes** de escrever no Windows. Confere
o valor após a escrita e registra o resultado em `history.jsonl`. Uma falha de
aplicação provoca tentativa de rollback. A restauração automática rejeita valores
mudados por outro programa após o FlashFix; a página de ajuste oferece a opção
de restaurar o valor salvo somente após confirmação explícita.
Cada ação é individual; a página Histórico oferece restauração em lote e mostra
progresso por ajuste concluído.

## Mouse e teclado

Os quatro ajustes usam a API documentada `SystemParametersInfoW` do Windows:

| Ajuste | Valor aplicado | Efeito esperado |
| --- | --- | --- |
| Aceleração do ponteiro | limites e aceleração `0,0,0` | movimento mais previsível do cursor; jogos com entrada bruta podem ignorar |
| Velocidade do ponteiro | `10/20` | referência padrão do Windows |
| Atraso de repetição | `0/3` | tecla mantida começa a repetir mais cedo |
| Taxa de repetição | `31/31` | tecla mantida repete mais rápido |

Esses controles não prometem reduzir latência física de USB, mouse ou teclado.
O usuário vê o estado atual, impacto e risco antes de confirmar. Nenhum ajuste
é aplicado ao abrir a página. Referência:
[Microsoft: SystemParametersInfoW](https://learn.microsoft.com/en-us/windows/win32/api/winuser/nf-winuser-systemparametersinfow).

## Sistema: diagnóstico inicial

O botão **Analisar sistema** captura um snapshot em segundo plano e monta um
resumo de Windows, formato do PC, fornecedores de CPU/GPU, faixa de RAM, tipo
de armazenamento e conexão. As recomendações são específicas ao detectado e
não executam alterações. Planos de energia, ajustes de driver, rede e limpeza
de aplicativos continuam pendentes de implementação e validação; a análise
não apresenta um botão de otimização em um clique sem operações justificadas.

## Verificação

- A solução compilou sem erros ou avisos após fechar o executável em uso.
- Os quatro testes do motor passaram: aplicação e restauração, conflito após
  alteração externa, rollback após falha simulada e persistência do backup
  entre instâncias.
- No app, login, Mouse, Teclado e Histórico abriram sem erro. A leitura mostrou
  dois valores reais de mouse, com velocidade atual `4/20`, sem aplicação.
- O diagnóstico identificou no PC de desenvolvimento Windows 10 Home,
  desktop, Intel, NVIDIA, até 16 GB de RAM, SSD e Ethernet. A página informou
  que nenhuma configuração foi alterada.

As escritas Win32 em hardware real ainda exigem teste controlado antes de
distribuição. Os testes de estado do motor usam um dispositivo simulado.
