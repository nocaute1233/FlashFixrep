namespace FlashFix.Optimization;

public enum TweakKind { MouseAcceleration, MouseSpeed, KeyboardDelay, KeyboardSpeed }

public sealed record TweakDefinition(
    string Id, string Category, string Name, string Description,
    string ExpectedImpact, string Risk, TweakKind Kind, int[] Target,
    string DocumentationUrl);

public static class TweakCatalog
{
    public static IReadOnlyList<TweakDefinition> All { get; } =
    [
        new("mouse.acceleration", "mouse", "Desativar aceleração do ponteiro",
            "Mantém uma relação mais previsível entre o movimento do mouse e o cursor do Windows. Jogos com entrada bruta podem ignorar esta opção.",
            "Consistência do cursor; não é uma redução comprovada de latência.", "Baixo",
            TweakKind.MouseAcceleration, [0, 0, 0],
            "https://learn.microsoft.com/windows/win32/api/winuser/nf-winuser-systemparametersinfow"),
        new("mouse.speed", "mouse", "Velocidade padrão do ponteiro",
            "Define a velocidade do ponteiro do Windows como 10/20. A sensibilidade dentro dos jogos não é alterada.",
            "Referência neutra e reversível para o cursor.", "Baixo",
            TweakKind.MouseSpeed, [10],
            "https://learn.microsoft.com/windows/win32/api/winuser/nf-winuser-systemparametersinfow"),
        new("keyboard.delay", "keyboard", "Menor atraso de repetição",
            "Reduz o tempo antes de uma tecla mantida começar a repetir. Não acelera o primeiro pressionamento.",
            "Resposta mais rápida em texto e navegação.", "Baixo",
            TweakKind.KeyboardDelay, [0],
            "https://learn.microsoft.com/windows/win32/api/winuser/nf-winuser-systemparametersinfow"),
        new("keyboard.speed", "keyboard", "Maior taxa de repetição",
            "Aumenta a frequência de repetição de uma tecla mantida. Não aumenta a taxa de leitura USB.",
            "Repetição mais rápida em texto e navegação.", "Baixo",
            TweakKind.KeyboardSpeed, [31],
            "https://learn.microsoft.com/windows/win32/api/winuser/nf-winuser-systemparametersinfow")
    ];

    public static TweakDefinition Get(string id) =>
        All.FirstOrDefault(x => x.Id == id) ?? throw new ArgumentException("Ajuste não encontrado.", nameof(id));
}
