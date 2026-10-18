using System.Text;
using System.Text.RegularExpressions;

namespace Lcs.Logi;

/// <summary>
/// Redakcja tekstu przed kolejką (25 § 7): usuwa znaki sterujące i separatory linii, maskuje typowe sekrety
/// (hasło=…, nagłówki Authorization/Bearer, JWT, bloki kluczy prywatnych PEM), ścieżki profilu użytkownika i adresy IPv4,
/// skraca do limitu. Podstawą ochrony jest lista dozwolonych pól; redakcja wartości jest ochroną dodatkową
/// (np. dla tekstu wyjątku, którego treści moduł nie kontroluje).
/// </summary>
public static class Redakcja
{
    public const string Znacznik = "[ZREDAGOWANO]";

    const RegexOptions O = RegexOptions.CultureInvariant | RegexOptions.Compiled;
    static readonly TimeSpan Limit = TimeSpan.FromMilliseconds(50);

    static readonly Regex Pem = new(@"-----BEGIN [A-Z0-9 ]*PRIVATE KEY-----[\s\S]*?(-----END [A-Z0-9 ]*PRIVATE KEY-----|$)", O, Limit);
    static readonly Regex Naglowek = new(@"(?i)\b(Bearer|Basic|Digest|Negotiate|NTLM)\s+[A-Za-z0-9\-._~+/=]{6,}", O, Limit);
    static readonly Regex Jwt = new(@"\beyJ[A-Za-z0-9_-]{5,}\.[A-Za-z0-9_-]{5,}\.[A-Za-z0-9_-]*", O, Limit);
    static readonly Regex KluczWartosc = new(
        @"(?i)\b([A-Za-z_]*(password|passwd|pwd|haslo|hasło|secret|sekret|token|apikey|api_key|api-key|cookie|authorization|connectionstring|recoverycode|enrollmentcode|privatekey)[A-Za-z_-]*)(\s*[:=]\s*)(""[^""]*""|'[^']*'|[^\s;,&]+)",
        O, Limit);
    static readonly Regex ProfilWindows = new(@"(?i)\b([A-Z]:\\(Users|Documents and Settings))\\[^\\/:*?""<>|\r\n\t ]+", O, Limit);
    static readonly Regex ProfilUnix = new(@"(/home|/Users)/[^/\s:]+", O, Limit);
    static readonly Regex Ipv4 = new(@"\b(25[0-5]|2[0-4]\d|1\d\d|[1-9]?\d)(\.(25[0-5]|2[0-4]\d|1\d\d|[1-9]?\d)){3}\b", O, Limit);
    /// <summary>Szybkie sprawdzenie, czy tekst może zawierać coś do maskowania (żeby nie uruchamiać wszystkich wzorców).</summary>
    static readonly Regex Wyzwalacz = new(@"(?i)-----BEGIN|bearer|basic|digest|negotiate|ntlm|eyJ|passw|pwd|has[lł]o|secret|sekret|token|api.?key|cookie|authorization|connectionstring|recoverycode|enrollmentcode|privatekey|\\Users\\|Documents and Settings|/home/|/Users/|\d\.\d", O, Limit);

    /// <summary>Wartość tekstowa pola: znaki sterujące → spacja, maskowanie sekretów, limit 512 znaków.</summary>
    public static string Tekst(string s, int max = KontraktLogu.MaxZnakowTekstu) => Przytnij(Maskuj(Sterujace(s, false)), max);

    /// <summary>Tekst wyjątku: zachowuje podział linii (zakodowany potem w JSON), maskuje sekrety, limit 4096 znaków.</summary>
    public static string Wyjatek(Exception ex)
    {
        if (ex == null) return null;
        string s;
        try { s = ex.ToString(); }
        catch (Exception e2) { s = ex.GetType().FullName + " (ToString nie powiódł się: " + e2.GetType().Name + ")"; }
        // Najpierw limit (maskowanie działa na najwyżej 4096 znakach), potem redakcja i ponowny limit (znacznik może wydłużyć tekst).
        // Blok PEM ucięty w połowie jest maskowany do końca tekstu.
        s = Przytnij(s, KontraktLogu.MaxZnakowWyjatku);
        return Przytnij(Maskuj(Sterujace(s, true)), KontraktLogu.MaxZnakowWyjatku);
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

    /// <summary>Maskowanie sekretów, profilu użytkownika i IPv4. Przekroczenie czasu wzorca daje cały tekst zamaskowany.</summary>
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
            s = Ipv4.Replace(s, "[IP]");
            return s;
        }
        catch (RegexMatchTimeoutException) { return Znacznik; }
    }

    public static string Przytnij(string s, int max)
    {
        if (s == null || s.Length <= max) return s;
        int n = max - 1;
        if (n > 0 && char.IsHighSurrogate(s[n - 1])) n--;
        return s.Substring(0, n) + "…";
    }
}
