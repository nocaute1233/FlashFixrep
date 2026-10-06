# Fase 1 — fundação

## Decisões

- A aplicação usa WinUI 3 e .NET 10. A UI é separada do catálogo de secções em
  `FlashFix.Core`.
- A navegação é construída a partir de `SectionCatalog`. Cada secção possui ID
  estável, título, descrição, ícone e fase prevista.
- Os tokens visuais ficam em `Design/Theme.xaml`. O estilo privilegia preto,
  branco, cinza, hierarquia tipográfica e espaçamento regular.
- Páginas de funcionalidades futuras mostram claramente o seu estado. Não há
  controlos que aparentem aplicar uma otimização sem implementar a operação.
- O FlashFix não executa código nem importa assets do projeto de referência.

## Próximos limites de componentes

| Componente | Responsabilidade |
| --- | --- |
| Desktop | Interface, navegação, notificações e preferências locais |
| Core | Contratos e regras de aplicação sem dependência de WinUI |
| Api | Contas, sessões, licenças e auditoria no servidor |
| Data | Migrações e persistência PostgreSQL |
| Hardware | Leituras Windows com resultados tipados e erros explícitos |
| Optimization | Catálogo, elegibilidade, execução, verificação e rollback |
| WindowsExecutor | Operações Windows permitidas, elevação e validação de parâmetros |
| BackupRestore | Estado anterior, checkpoints e histórico local |
| AimTrainer | Modos, motor de sessão e métricas |
| Crosshair / Display | Perfis e integrações suportadas |
| KeyManager | Ferramenta administrativa com autenticação própria |

## Regra para cada otimização

Uma operação só entrará no catálogo com evidência técnica, compatibilidade
declarada, leitura do estado original, aplicação, verificação, rollback e
registo do resultado. O engine recusará uma operação quando não conseguir
guardar os valores necessários para a reverter. Nenhum comando do `.bat` de
referência é aceite automaticamente.

## Verificação da Fase 1

- Compilação Debug sem erros ou avisos.
- Arranque no Windows 10 de desenvolvimento, usando build sem pacote.
- Visão Geral e navegação para as secções funcionam.
- Nenhuma ação altera configurações do Windows.
