using System.Reflection;
using System.Runtime.Versioning;
using System.Text;
using System.Text.Json;
using LcsWidzew.Sim;

namespace Lcs.Core.TestHost;

internal static class BoundaryVerification
{
    /// <summary>Sprawdza rzeczywiste referencje wszystkich zależności rdzenia, również niewykonane ścieżki kodu.</summary>
    internal static string Run()
    {
        var core = typeof(Simulator).Assembly;
        string target = core.GetCustomAttribute<TargetFrameworkAttribute>()?.FrameworkName;
        if (target != ".NETCoreApp,Version=v8.0") throw new InvalidOperationException("Rdzeń nie ma neutralnego celu .NET 8: " + target);
        var visited = new SortedSet<string>(StringComparer.Ordinal);
        var pending = new Queue<Assembly>();
        pending.Enqueue(core);
        while (pending.TryDequeue(out var assembly))
        {
            if (!visited.Add(assembly.GetName().Name)) continue;
            if (Forbidden(assembly.GetName().Name)) throw new InvalidOperationException("Zależność interfejsu w rdzeniu: " + assembly.FullName);
            foreach (var reference in assembly.GetReferencedAssemblies())
            {
                if (Forbidden(reference.Name)) throw new InvalidOperationException($"Zależność interfejsu {reference.Name} w {assembly.GetName().Name}");
                pending.Enqueue(Assembly.Load(reference));
            }
        }
        // Cel runtime procesu także nie może pobierać WindowsDesktop.
        string runtimeConfig = Path.Combine(AppContext.BaseDirectory, "Lcs.Core.TestHost.runtimeconfig.json");
        using (var document = JsonDocument.Parse(File.ReadAllText(runtimeConfig)))
        {
            var options = document.RootElement.GetProperty("runtimeOptions");
            if (options.GetProperty("tfm").GetString() != "net8.0") throw new InvalidOperationException("Host wymaga celu net8.0.");
            if (File.ReadAllText(runtimeConfig).Contains("WindowsDesktop", StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException("Host wymaga WindowsDesktop.");
        }
        var report = new StringBuilder();
        report.AppendLine("GRANICA RDZENIA: OK");
        report.AppendLine("TargetFramework rdzenia: " + target);
        report.AppendLine("TargetFramework hosta: net8.0; WindowsDesktop: brak");
        report.AppendLine("Sprawdzone referencje przechodnie rdzenia: " + visited.Count);
        foreach (string assembly in visited) report.AppendLine("  " + assembly);
        return report.ToString();
    }

    static bool Forbidden(string name) => name is "WindowsBase" or "System.Xaml" ||
        name.StartsWith("Presentation", StringComparison.Ordinal) || name.StartsWith("System.Windows", StringComparison.Ordinal) ||
        name.StartsWith("UIAutomation", StringComparison.Ordinal) || name.Contains("WindowsDesktop", StringComparison.OrdinalIgnoreCase);
}
