using System.Buffers;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;

namespace LcsWidzew.Sim;

/// <summary>
/// Zapis kanonicznych artefaktów KZ (S02A.02: <see cref="WejscieCyklu"/>, <see cref="TrasaWejsc"/>) niezależny od biblioteki JSON.
/// Ciągi według RFC 8785 §3.2.2.2 (JCS, jak ECMAScript JSON.stringify i Python <c>json.dumps(ensure_ascii=False)</c>): ucieczka
/// wyłącznie cudzysłowu, ukośnika wstecznego i znaków U+0000–U+001F (\b \t \n \f \r, pozostałe \u00xx małymi literami);
/// wszystkie inne znaki (także U+007F–U+009F, U+2028, U+FEFF, spoza BMP) dosłownie w UTF-8. Samotny surogat UTF-16 – \udxxx jak
/// w JCS (kontrakt odrzuca takie identyfikatory). Dzięki temu niezależna implementacja (skrypt kontraktów, w przyszłości Ada)
/// odtwarza bajty i SHA-256 bez znajomości .NET. Liczby wyłącznie całkowite (zapis niezmienny kulturowo).
/// </summary>
internal static class KanonJson
{
    const string Hex = "0123456789abcdef";

    /// <summary>Znaki, które mogą wymagać ucieczki: U+0000–U+001F, cudzysłów, ukośnik wsteczny i każdy surogat UTF-16.</summary>
    static readonly SearchValues<char> MozeWymagacUcieczki = SearchValues.Create(
        Enumerable.Range(0, 0x20).Select(i => (char)i).Concat(new[] { '"', '\\' }).Concat(Enumerable.Range(0xD800, 0x800).Select(i => (char)i)).ToArray());

    /// <summary>Ciąg JSON w cudzysłowie; null – literał <c>null</c>.</summary>
    internal static void Tekst(StringBuilder sb, string s)
    {
        if (s == null) { sb.Append("null"); return; }
        // Szybka ścieżka (te same bajty): bez znaków wymagających ucieczki pętla niżej dopisałaby cały ciąg bez zmian.
        if (s.AsSpan().IndexOfAny(MozeWymagacUcieczki) < 0) { sb.Append('"').Append(s).Append('"'); return; }
        sb.Append('"');
        int start = 0;
        for (int i = 0; i < s.Length; i++)
        {
            char c = s[i];
            string krotka = c switch { '"' => "\\\"", '\\' => "\\\\", '\b' => "\\b", '\t' => "\\t", '\n' => "\\n", '\f' => "\\f", '\r' => "\\r", _ => null };
            bool samotnySurogat = char.IsHighSurrogate(c) ? i + 1 >= s.Length || !char.IsLowSurrogate(s[i + 1]) : char.IsLowSurrogate(c) && (i == 0 || !char.IsHighSurrogate(s[i - 1]));
            if (krotka == null && c >= 0x20 && !samotnySurogat) continue;
            sb.Append(s, start, i - start);
            if (krotka != null) sb.Append(krotka);
            else sb.Append("\\u").Append(Hex[c >> 12]).Append(Hex[(c >> 8) & 0xF]).Append(Hex[(c >> 4) & 0xF]).Append(Hex[c & 0xF]);
            start = i + 1;
        }
        sb.Append(s, start, s.Length - start).Append('"');
    }

    /// <summary>Para „,"nazwa":” (bez spacji – zapis jednoliniowy) albo „"nazwa":” dla pierwszego pola obiektu.</summary>
    internal static StringBuilder Pole(StringBuilder sb, string nazwa, bool pierwsze = false)
    {
        if (!pierwsze) sb.Append(',');
        Tekst(sb, nazwa);
        return sb.Append(':');
    }

    internal static void Logiczna(StringBuilder sb, string nazwa, bool v) => Pole(sb, nazwa).Append(v ? "true" : "false");
    internal static void Logiczna(StringBuilder sb, string nazwa, bool? v) => Pole(sb, nazwa).Append(v is bool b ? (b ? "true" : "false") : "null");
    internal static void Liczba(StringBuilder sb, string nazwa, long v, bool pierwsze = false) => Pole(sb, nazwa, pierwsze).Append(v.ToString(CultureInfo.InvariantCulture));
    internal static void Tekst(StringBuilder sb, string nazwa, string v) { Pole(sb, nazwa); Tekst(sb, v); }

    [ThreadStatic] static IncrementalHash skrot;
    [ThreadStatic] static Encoder koder;
    [ThreadStatic] static byte[] bajty;

    /// <summary>
    /// SHA-256 (hex, małe litery) bajtów UTF-8 zawartości bufora – wynik identyczny z
    /// <c>SHA256.HashData(Encoding.UTF8.GetBytes(sb.ToString()))</c>, bez kopii napisu i tablicy (wydajność S02A.03).
    /// Koder zachowuje stan między fragmentami, więc para surogatów na styku fragmentów jest kodowana poprawnie.
    /// </summary>
    internal static string Sha256(StringBuilder sb)
    {
        skrot ??= IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        koder ??= new UTF8Encoding(false).GetEncoder();
        koder.Reset();
        bajty ??= new byte[16384];
        foreach (var fragment in sb.GetChunks())
        {
            int max = Encoding.UTF8.GetMaxByteCount(fragment.Length);
            if (bajty.Length < max) bajty = new byte[max];
            skrot.AppendData(bajty, 0, koder.GetBytes(fragment.Span, bajty, flush: false));
        }
        int reszta = koder.GetBytes(ReadOnlySpan<char>.Empty, bajty, flush: true);
        if (reszta > 0) skrot.AppendData(bajty, 0, reszta);
        Span<byte> h = stackalloc byte[32];
        skrot.GetHashAndReset(h);
        return Convert.ToHexStringLower(h);
    }
}
