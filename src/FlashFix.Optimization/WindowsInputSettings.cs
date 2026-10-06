using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;

namespace FlashFix.Optimization;

public interface IInputSettings
{
    int[] Read(TweakKind kind);
    void Write(TweakKind kind, int[] values);
}

[SupportedOSPlatform("windows")]
public sealed class WindowsInputSettings : IInputSettings
{
    private const uint PersistAndBroadcast = 0x01 | 0x02;

    public int[] Read(TweakKind kind)
    {
        if (kind == TweakKind.MouseAcceleration)
        {
            var values = new int[3];
            if (!SystemParametersInfoArray(0x0003, 0, values, 0)) Throw();
            return values;
        }

        uint action = kind switch
        {
            TweakKind.MouseSpeed => 0x0070,
            TweakKind.MouseWheelLines => 0x0068,
            TweakKind.MouseWheelChars => 0x006C,
            TweakKind.KeyboardDelay => 0x0016,
            TweakKind.KeyboardSpeed => 0x000A,
            _ => throw new ArgumentOutOfRangeException(nameof(kind))
        };
        if (!SystemParametersInfoOut(action, 0, out var value, 0)) Throw();
        return [value];
    }

    public void Write(TweakKind kind, int[] values)
    {
        ArgumentNullException.ThrowIfNull(values);
        bool result = kind switch
        {
            TweakKind.MouseAcceleration when values.Length == 3 =>
                SystemParametersInfoArray(0x0004, 0, values, PersistAndBroadcast),
            TweakKind.MouseSpeed when values.Length == 1 && values[0] is >= 1 and <= 20 =>
                SystemParametersInfoPointer(0x0071, 0, new IntPtr(values[0]), PersistAndBroadcast),
            TweakKind.MouseWheelLines when values.Length == 1 && (values[0] >= 0 || values[0] == -1) =>
                SystemParametersInfoPointer(0x0069, unchecked((uint)values[0]), IntPtr.Zero, PersistAndBroadcast),
            TweakKind.MouseWheelChars when values.Length == 1 && (values[0] >= 0 || values[0] == -1) =>
                SystemParametersInfoPointer(0x006D, unchecked((uint)values[0]), IntPtr.Zero, PersistAndBroadcast),
            TweakKind.KeyboardDelay when values.Length == 1 && values[0] is >= 0 and <= 3 =>
                SystemParametersInfoPointer(0x0017, (uint)values[0], IntPtr.Zero, PersistAndBroadcast),
            TweakKind.KeyboardSpeed when values.Length == 1 && values[0] is >= 0 and <= 31 =>
                SystemParametersInfoPointer(0x000B, (uint)values[0], IntPtr.Zero, PersistAndBroadcast),
            _ => throw new ArgumentException("Valor inválido para este ajuste.", nameof(values))
        };
        if (!result) Throw();
    }

    private static void Throw() => throw new Win32Exception(Marshal.GetLastWin32Error());

    [DllImport("user32.dll", EntryPoint = "SystemParametersInfoW", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool SystemParametersInfoArray(uint action, uint parameter,
        [In, Out] int[] values, uint flags);

    [DllImport("user32.dll", EntryPoint = "SystemParametersInfoW", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool SystemParametersInfoOut(uint action, uint parameter,
        out int value, uint flags);

    [DllImport("user32.dll", EntryPoint = "SystemParametersInfoW", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool SystemParametersInfoPointer(uint action, uint parameter,
        IntPtr value, uint flags);
}
