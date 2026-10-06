namespace FlashFix.Core.Navigation;

public sealed record SectionDefinition(
    string Id,
    string Title,
    string Subtitle,
    string Description,
    string Glyph,
    string Phase);

/// <summary>Single source of truth for the Phase 1 navigation shell.</summary>
public static class SectionCatalog
{
    public static IReadOnlyList<SectionDefinition> Items { get; } =
    [
        new("overview", "Visão Geral", "Informações do computador em um só lugar.", "Consulte os componentes e os indicadores de uso coletados pelo Windows.", "\uE80F", "Disponível"),
        new("system", "Sistema", "Análise do hardware.", "Identifique os componentes e leia recomendações para este computador, sem alterações automáticas.", "\uE9CE", "Disponível"),
        new("mouse", "Mouse", "Configurações do ponteiro.", "Consulte o valor atual de cada ajuste e restaure o anterior quando necessário.", "\uE962", "Disponível"),
        new("keyboard", "Teclado", "Repetição de teclas.", "Ajuste o atraso e a velocidade de repetição de teclas mantidas.", "\uE765", "Disponível"),
        new("display", "Display", "Resolução, frequência e opções do Windows.", "Confira a tela detectada e abra as configurações do Windows para revisar resolução, escala e taxa de atualização.", "\uE7F4", "Disponível"),
        new("crosshair", "Crosshair", "Editor de mira e perfis.", "Crie e salve perfis com prévia dentro do FlashFix. A sobreposição em jogos ainda não está disponível.", "\uE81E", "Parcial"),
        new("trainer", "Aim Trainer", "Treinos de mira dentro do FlashFix.", "Flick, Tracking, Reflex Shot e Gridshot com resultados salvos no computador.", "\uE7C1", "Disponível"),
        new("history", "Histórico", "Alterações e restauração.", "Confira o resultado dos ajustes e restaure os valores anteriores salvos pelo FlashFix.", "\uE81C", "Disponível")
    ];

    public static SectionDefinition Get(string id) =>
        Items.First(item => item.Id == id);
}
