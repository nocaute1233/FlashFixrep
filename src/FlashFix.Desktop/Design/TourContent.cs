namespace FlashFix_Desktop.Design;

public sealed record TourStep(string SectionId, string Title, string Message);

public static class TourContent
{
    public static IReadOnlyList<TourStep> Steps { get; } =
    [
        new("overview", "Olá, eu sou o Flash.",
            "Vou passar pelos menus e explicar o que já funciona. Use Avançar para seguir ou Pular se preferir explorar por conta própria."),
        new("system", "Comece pela análise do sistema.",
            "Aqui você identifica processador, placa de vídeo, memória, armazenamento e rede. A análise apresenta recomendações e não altera o Windows."),
        new("mouse", "Ajustes de mouse.",
            "Esta seção mostra os valores atuais do ponteiro. Antes de aplicar um ajuste, o FlashFix salva o valor original para permitir a restauração."),
        new("keyboard", "Ajustes de teclado.",
            "Você pode mudar o atraso e a velocidade de repetição de teclas mantidas. Esses controles não alteram a latência do primeiro pressionamento."),
        new("display", "Display.",
            "O controle de cor por fabricante está planejado. Esta página ainda não altera a imagem; ela será liberada depois da validação em hardware compatível."),
        new("crosshair", "Crosshair.",
            "Crie uma mira na prévia e salve perfis para usar depois. A sobreposição sobre jogos ainda não está disponível."),
        new("trainer", "Aim Trainer.",
            "Escolha entre Flick, Tracking, Reflex Shot e Gridshot. O treino roda nesta janela e salva a pontuação e a precisão ao terminar."),
        new("history", "Histórico e restauração.",
            "Consulte as alterações feitas pelo FlashFix e restaure os valores salvos. A restauração em lote aparece quando há ajustes aplicados."),
        new("settings", "Sua conta e preferências.",
            "Veja a licença e ajuste as animações. Para rever este percurso, escolha Apresentação do Flash no menu lateral.")
    ];
}
