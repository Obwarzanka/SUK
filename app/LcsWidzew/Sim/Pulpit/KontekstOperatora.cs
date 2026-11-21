namespace LcsWidzew.Sim;

/// <summary>
/// Migawka stanowiska na wejściu operacji. Same wartości, bez odwołania do mutable Desk/User.
/// Nie jest potwierdzeniem tożsamości ani praw przyszłego protokołu sieciowego.
/// </summary>
public sealed record KontekstOperatora(string DeskId, string Page, string Nazwa, string Rola, bool Zalogowany);
