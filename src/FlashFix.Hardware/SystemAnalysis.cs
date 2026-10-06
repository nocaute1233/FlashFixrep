namespace FlashFix.Hardware;

public sealed record AnalysisItem(string Area, string Detected, string Recommendation);

public sealed record SystemAnalysis(DateTime CapturedAt, string Summary, IReadOnlyList<AnalysisItem> Items);

public static class SystemAnalyzer
{
    public static SystemAnalysis Analyze(HardwareSnapshot hardware)
    {
        var laptop = hardware.FormFactor == "Notebook";
        var cpuVendor = Vendor(hardware.Cpu, "Intel", "AMD");
        var gpuVendor = Vendor(hardware.Gpu, "NVIDIA", "AMD", "Intel");
        var memoryGb = ParseGigabytes(hardware.Memory);
        var memoryTier = memoryGb switch
        {
            <= 0 => "Capacidade não identificada",
            <= 4 => "Até 4 GB",
            <= 8 => "Até 8 GB",
            <= 16 => "Até 16 GB",
            <= 32 => "Até 32 GB",
            _ => "Mais de 32 GB"
        };
        var storage = hardware.StorageType.Contains("SSD", StringComparison.OrdinalIgnoreCase)
            ? hardware.StorageType.Contains("HDD", StringComparison.OrdinalIgnoreCase) ? "SSD + HDD" : "SSD"
            : hardware.StorageType.Contains("HDD", StringComparison.OrdinalIgnoreCase) ? "HDD" : "Não identificado";
        var connection = hardware.Network.StartsWith("Ethernet", StringComparison.OrdinalIgnoreCase)
            ? "Ethernet" : hardware.Network.StartsWith("Wi-Fi", StringComparison.OrdinalIgnoreCase)
                ? "Wi-Fi" : "Não identificada";
        var items = new List<AnalysisItem>
        {
            new("ENERGIA", hardware.FormFactor,
                laptop ? "Prefira um perfil equilibrado e confira temperatura e bateria antes de exigir desempenho máximo."
                    : "Um perfil de desempenho pode ser útil em cargas sustentadas; mantenha a gestão dinâmica do processador."),
            new("PROCESSADOR", $"{cpuVendor} · {hardware.Cpu}",
                "Nenhuma mudança de scheduler, timer ou prioridade será sugerida sem evidência e medição neste PC."),
            new("PLACA DE VÍDEO", $"{gpuVendor} · {hardware.Gpu}",
                "Ajustes de driver devem ser feitos no painel oficial do fabricante e validados por jogo."),
            new("MEMÓRIA", $"{hardware.Memory} · {memoryTier}",
                memoryGb is > 0 and <= 8
                    ? "Monitore o uso durante os jogos e feche aplicativos pesados se houver pressão de memória. Preserve o arquivo de paginação gerenciado pelo Windows."
                    : "Monitore o uso real. Alterações agressivas de memória virtual não são recomendadas."),
            new("ARMAZENAMENTO", $"{storage} · {hardware.Storage}",
                storage == "HDD" ? "Mantenha espaço livre e use as ferramentas de otimização do Windows quando necessário."
                    : "Mantenha espaço livre. O Windows gerencia a otimização da unidade; não aplique ajustes antigos de HDD ao SSD."),
            new("REDE", $"{connection} · {hardware.Network}",
                connection == "Wi-Fi" ? "Se possível, compare com Ethernet em uma medição real de estabilidade e latência."
                    : "Mantenha driver atualizado e meça a conexão antes de alterar parâmetros TCP."),
            new("WINDOWS", hardware.Windows,
                "Preserve Defender, firewall, Windows Update e os serviços essenciais. Remoção de aplicativos deve ser individual e reversível.")
        };
        return new SystemAnalysis(hardware.CapturedAt,
            $"{hardware.Windows} · {hardware.FormFactor} · {cpuVendor} + {gpuVendor} · {memoryTier} · {storage} · {connection}",
            items);
    }

    private static string Vendor(string value, params string[] names) =>
        names.FirstOrDefault(name => value.Contains(name, StringComparison.OrdinalIgnoreCase)) ?? "Não identificado";

    private static double ParseGigabytes(string value)
    {
        var number = value.Split(' ')[0];
        return double.TryParse(number, System.Globalization.NumberStyles.Float,
            System.Globalization.CultureInfo.CurrentCulture, out var size) ||
            double.TryParse(number, System.Globalization.NumberStyles.Float,
                System.Globalization.CultureInfo.InvariantCulture, out size) ? size : 0;
    }
}
