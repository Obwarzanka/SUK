using System.Text;
using System.Text.RegularExpressions;
using System.Net;
using System.Net.Sockets;

namespace Lcs.Logi;

/// <summary>
/// Redakcja tekstu przed kolejką (25 § 7): usuwa znaki sterujące i separatory linii, maskuje typowe sekrety
    /// (hasło=…, nagłówki Authorization/Bearer, JWT, bloki kluczy prywatnych PEM), ścieżki profilu użytkownika i adresy IP,
/// skraca do limitu. Podstawą ochrony jest lista dozwolonych pól; redakcja wartości jest ochroną dodatkową
/// (np. dla tekstu wyjątku, którego treści moduł nie kontroluje).
/// </summary>
public static class Redakcja
{
    public const string Znacznik = "[ZREDAGOWANO]";

    // Limit wejścia obowiązuje przed usuwaniem znaków sterujących i wzorcami. Duży komunikat nie może
    // wymusić kopii całego tekstu na wątku emitującym; po redakcji obowiązują nadal limity 512/4096.
    internal const int MaxZnakowWejscia = KontraktLogu.MaxZnakowWyjatku;
    const int MaxWyjatkow = 8;

    const RegexOptions O = RegexOptions.CultureInvariant | RegexOptions.Compiled;
    static readonly TimeSpan Limit = TimeSpan.FromMilliseconds(50);

    static readonly Regex Pem = new(@"-----BEGIN [A-Z0-9 ]*PRIVATE KEY-----[\s\S]*?(-----END [A-Z0-9 ]*PRIVATE KEY-----|$)", O, Limit);
    static readonly Regex Naglowek = new(@"(?i)\b(Bearer|Basic|Digest|Negotiate|NTLM)\s+[A-Za-z0-9\-._~+/=]+", O, Limit);
    static readonly Regex Jwt = new(@"\beyJ[A-Za-z0-9_-]{5,}\.[A-Za-z0-9_-]{5,}\.[A-Za-z0-9_-]*", O, Limit);
    static readonly Regex KluczWartosc = new(
        """(?i)\b([A-Za-z_]*(password|passwd|pwd|haslo|hasło|secret|sekret|token|apikey|api_key|api-key|cookie|authorization|connectionstring|recoverycode|enrollmentcode|privatekey)[A-Za-z_-]*)(["']?\s*[:=]\s*)("(?:\\.|[^"\\])*(?:"|$)|'(?:\\.|[^'\\])*(?:'|$)|[^\s;,&]+)""",
        O, Limit);
    static readonly Regex ProfilWindows = new(@"(?i)\b([A-Z]:\\(Users|Documents and Settings))\\[^\\/:*?""'<>|\r\n\t]+", O, Limit);
    static readonly Regex ProfilUnix = new(@"(/home|/Users)/[^/\r\n\t:""']+", O, Limit);
    // Wzorzec wybiera tylko kandydatów; poprawność IPv6 potwierdza parser adresów, bez DNS i I/O.
    static readonly Regex Ipv6 = new(@"(?<![A-Za-z0-9_])\[?(?<adres>(?:[0-9A-Fa-f]{0,4}:){2,}[0-9A-Fa-f:.]*(?:%[A-Za-z0-9_.-]+)?)\]?", O, Limit);
    static readonly Regex Godzina = new(@"^[0-2]?[0-9]:[0-5][0-9]:[0-5][0-9](?:\.[0-9]+)?$", O, Limit);
    static readonly Regex Ipv4 = new(@"\b(25[0-5]|2[0-4]\d|1\d\d|[1-9]?\d)(\.(25[0-5]|2[0-4]\d|1\d\d|[1-9]?\d)){3}\b", O, Limit);
    /// <summary>Szybkie sprawdzenie, czy tekst może zawierać coś do maskowania (żeby nie uruchamiać wszystkich wzorców).</summary>
    static readonly Regex Wyzwalacz = new(@"(?i)-----BEGIN|bearer|basic|digest|negotiate|ntlm|eyJ|passw|pwd|has[lł]o|secret|sekret|token|api.?key|cookie|authorization|connectionstring|recoverycode|enrollmentcode|privatekey|\\Users\\|Documents and Settings|/home/|/Users/|\d\.\d|:", O, Limit);

    /// <summary>Wartość tekstowa pola: znaki sterujące → spacja, maskowanie sekretów, limit 512 znaków.</summary>
    public static string Tekst(string s, int max = KontraktLogu.MaxZnakowTekstu) =>
        Przytnij(Maskuj(Sterujace(Przytnij(s, MaxZnakowWejscia), false)), Math.Clamp(max, 1, KontraktLogu.MaxZnakowTekstu));

    /// <summary>Tekst wyjątku: zachowuje podział linii (zakodowany potem w JSON), maskuje sekrety, limit 4096 znaków.</summary>
    public static string Wyjatek(Exception ex)
    {
        if (ex == null) return null;
        // Exception.ToString() najpierw tworzy pełny tekst całego grafu wyjątków (AggregateException również
        // w Message). Zamiast tego kopiujemy ograniczone fragmenty, maks. 8 wyjątków i miejsce rzucenia.
        // TargetSite w CoreCLR pobiera jedną metodę; StackTrace materializowałby cały stos przed cięciem.
        // Celowo pomijamy pełny stos, ścieżki plikowe i numery wierszy oraz nadpisane ToString.
        var b = new StringBuilder(MaxZnakowWejscia);
        var oczekujace = new Stack<Exception>();
        oczekujace.Push(ex);
        for (int n = 0; n < MaxWyjatkow && oczekujace.Count > 0 && b.Length < MaxZnakowWejscia; n++)
        {
            var e = oczekujace.Pop();
            if (n > 0) Dopisz(b, Environment.NewLine + "---> ");
            Dopisz(b, e.GetType().FullName);
            if (e is AggregateException a)
            {
                Dopisz(b, ": wiele błędów");
                int ile = Math.Min(a.InnerExceptions.Count, MaxWyjatkow - n - 1 - oczekujace.Count);
                for (int i = ile - 1; i >= 0; i--) oczekujace.Push(a.InnerExceptions[i]);
            }
            else
            {
                try { Dopisz(b, ": "); Dopisz(b, e.Message); }
                catch (Exception) { Dopisz(b, "(wiadomość niedostępna)"); }
                if (e.InnerException != null && oczekujace.Count < MaxWyjatkow - n - 1) oczekujace.Push(e.InnerException);
            }
            if (b.Length >= MaxZnakowWejscia) break;
            try
            {
                var metoda = e.TargetSite;
                if (metoda != null)
                {
                    Dopisz(b, Environment.NewLine + "   at ");
                    Dopisz(b, metoda.DeclaringType?.FullName);
                    Dopisz(b, "."); Dopisz(b, metoda.Name);
                }
            }
            catch (Exception) { Dopisz(b, " (miejsce błędu niedostępne)"); }
        }
        string s = b.ToString();
        return Przytnij(Maskuj(Sterujace(s, true)), KontraktLogu.MaxZnakowWyjatku);
    }

    static void Dopisz(StringBuilder b, string s)
    {
        if (s == null || b.Length >= MaxZnakowWejscia) return;
        int ile = Math.Min(s.Length, MaxZnakowWejscia - b.Length);
        bool uciete = ile < s.Length;
        if (uciete) ile--;
        if (ile > 0 && ile < s.Length && char.IsHighSurrogate(s[ile - 1])) ile--;
        b.Append(s.AsSpan(0, ile));
        if (uciete) b.Append('…');
    }

    /// <summary>Usuwa znaki sterujące C0/C1, DEL i U+2028/2029; przy <paramref name="nowaLinia"/> zostawia CR, LF i TAB.</summary>
    public static string Sterujace(string s, bool nowaLinia)
    {
        if (string.IsNullOrEmpty(s)) return s;
        int i = 0;
        for (; i < s.Length; i++) if (Zly(s[i], nowaLinia)) break;
        if (i == s.Length) return s;
        var b = new StringBuilder(s.Length);
        b.Append(s, 0, i);
        for (; i < s.Length; i++) b.Append(Zly(s[i], nowaLinia) ? ' ' : s[i]);
        return b.ToString();
    }

    static bool Zly(char c, bool nowaLinia)
    {
        if (nowaLinia && (c == '\r' || c == '\n' || c == '\t')) return false;
        return c < ' ' || (c >= '\u007F' && c <= '\u009F') || c == '\u2028' || c == '\u2029';
    }

    /// <summary>Maskowanie sekretów, profilu użytkownika i adresów IP. Przekroczenie czasu wzorca daje cały tekst zamaskowany.</summary>
    public static string Maskuj(string s)
    {
        if (string.IsNullOrEmpty(s) || s.Length < 4) return s;
        try
        {
            if (!Wyzwalacz.IsMatch(s)) return s;
            s = Pem.Replace(s, Znacznik);
            s = Naglowek.Replace(s, m => m.Groups[1].Value + " " + Znacznik);
            s = Jwt.Replace(s, Znacznik);
            s = KluczWartosc.Replace(s, m => m.Groups[1].Value + m.Groups[3].Value + Znacznik);
            s = ProfilWindows.Replace(s, m => m.Groups[1].Value + "\\" + "[UZYTKOWNIK]");
            s = ProfilUnix.Replace(s, m => m.Groups[1].Value + "/[UZYTKOWNIK]");
            s = Ipv6.Replace(s, m => MaskujIpv6(m, s));
            s = Ipv4.Replace(s, "[IP]");
            return s;
        }
        catch (RegexMatchTimeoutException) { return Znacznik; }
    }

    static string MaskujIpv6(Match m, string tekst)
    {
        string adres = m.Groups["adres"].Value;
        if (IPAddress.TryParse(adres, out var ip) && ip.AddressFamily == AddressFamily.InterNetworkV6) return "[IP]";
        int koniec = m.Index + m.Length;
        bool przyGranicy = tekst.AsSpan(koniec).StartsWith("…", StringComparison.Ordinal)
            || tekst.AsSpan(koniec).StartsWith("...", StringComparison.Ordinal);
        // Cięcie może odebrać końcówkę poprawnego adresu. Wtedy parser odmawia, lecz pozostawienie
        // prefiksu ujawniałoby sieć. Kandydat przy granicy jest maskowany zachowawczo, z wyjątkiem godzin.
        return przyGranicy && !Godzina.IsMatch(adres) ? "[IP]" : m.Value;
    }

    public static string Przytnij(string s, int max)
    {
        if (s == null || s.Length <= max) return s;
        int n = max - 1;
        if (n > 0 && char.IsHighSurrogate(s[n - 1])) n--;
        return s.Substring(0, n) + "…";
    }
}
