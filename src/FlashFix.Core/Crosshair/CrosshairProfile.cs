namespace FlashFix.Core.Crosshair;

public sealed record CrosshairProfile(
    Guid Id, string Name, string Color, int ArmLength, int Thickness,
    int Gap, double Opacity, bool CenterDot, bool Outline)
{
    public static CrosshairProfile Default() =>
        new(Guid.NewGuid(), "Nova mira", "#FFFFFF", 15, 3, 5, 1, false, true);

    public void Validate()
    {
        if (string.IsNullOrWhiteSpace(Name) || Name.Trim().Length > 32)
            throw new ArgumentException("Dê um nome de até 32 caracteres ao perfil.");
        if (Color.Length != 7 || Color[0] != '#' ||
            !Color.AsSpan(1).ToString().All(Uri.IsHexDigit))
            throw new ArgumentException("Use uma cor hexadecimal no formato #RRGGBB.");
        if (ArmLength is < 4 or > 30 || Thickness is < 1 or > 8 ||
            Gap is < 0 or > 20 || Opacity is < 0.2 or > 1)
            throw new ArgumentException("Um dos controles da mira está fora do limite permitido.");
    }
}
