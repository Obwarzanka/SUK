using System.Security.Cryptography;
using System.Text;

namespace Lcs.Core.TestHost;

/// <summary>
/// Raport i dziennik są projekcjami domenowymi: zachowujemy polecenia/przyczyny odmów, sygnały, czas symulacji oraz kolejność.
/// Z porównania wyłączony jest wyłącznie osobny plik .postep z czasem wykonania procesu.
/// </summary>
internal static class RegressionComparison
{
    internal readonly record struct Result(bool Equal, string Report);

    internal static Result Compare(string reference, string actual, bool requireEvents)
    {
        var report = new StringBuilder();
        bool equal = CompareFile(reference, actual, "raport domenowy", report);
        bool referenceEvents = File.Exists(reference + ".zdarzenia"), actualEvents = File.Exists(actual + ".zdarzenia");
        if (referenceEvents && actualEvents)
            equal &= CompareFile(reference + ".zdarzenia", actual + ".zdarzenia", "pełny dziennik domenowy", report);
        else if (requireEvents || referenceEvents && !actualEvents)
        {
            report.AppendLine($"BRAK DZIENNIKA: baza={referenceEvents}, wynik={actualEvents}; wymagany={requireEvents}");
            equal = false;
        }
        else report.AppendLine("Pełny dziennik: brak w bazie (historyczny scenariusz); porównano pełny istniejący raport. Zgodność nowego dziennika wymaga osobnego --require-events.");
        report.Insert(0, $"PORÓWNANIE SEMANTYKI: {(equal ? "ZGODNE" : "RÓŻNICA")}\nNormalizacja: wyłącznie CRLF/CR do LF; kolejność i czasy domenowe zachowane.\n");
        return new Result(equal, report.ToString());
    }

    static bool CompareFile(string reference, string actual, string label, StringBuilder report)
    {
        if (!File.Exists(reference) || !File.Exists(actual))
        {
            report.AppendLine($"BRAK PLIKU ({label}): baza={File.Exists(reference)}, wynik={File.Exists(actual)}");
            return false;
        }
        string expectedText = Normalize(File.ReadAllText(reference)), actualText = Normalize(File.ReadAllText(actual));
        string expectedHash = Hash(expectedText), actualHash = Hash(actualText);
        bool equal = expectedText == actualText;
        report.AppendLine($"{label}: {(equal ? "ZGODNY" : "RÓŻNY")}; SHA256 baza={expectedHash}, wynik={actualHash}");
        if (!equal)
        {
            string[] expectedLines = expectedText.Split('\n'), actualLines = actualText.Split('\n');
            int first = 0, count = Math.Min(expectedLines.Length, actualLines.Length);
            while (first < count && expectedLines[first] == actualLines[first]) first++;
            report.AppendLine($"Pierwsza różnica w linii {first + 1}; linie baza={expectedLines.Length}, wynik={actualLines.Length}");
            report.AppendLine("  baza: " + (first < expectedLines.Length ? expectedLines[first] : "<koniec pliku>"));
            report.AppendLine("  wynik: " + (first < actualLines.Length ? actualLines[first] : "<koniec pliku>"));
        }
        return equal;
    }

    static string Normalize(string text) => text.Replace("\r\n", "\n", StringComparison.Ordinal).Replace('\r', '\n');
    static string Hash(string text) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(text))).ToLowerInvariant();
}
