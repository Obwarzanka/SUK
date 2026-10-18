using System.Text;
using Serilog.Events;
using Serilog.Formatting;

namespace Lcs.Logi;

/// <summary>
/// Nakładka na gotowy CompactJsonFormatter pilnująca limitu 8 KiB na rekord (B01) po serializacji. Rekord za duży jest najpierw
/// skracany (teksty do 128 znaków, wyjątek do 1024), a gdy to nie wystarcza – zastępowany rekordem LOG.RECORD_TOO_LARGE
/// z polami wymaganymi. Działa wyłącznie na wątku workera.
/// </summary>
internal sealed class OgraniczonyFormatter : ITextFormatter
{
    readonly ITextFormatter wewnetrzny;
    readonly int maxBajtow;
    readonly Action naObciecie;
    readonly Action<LogEvent, string> naZastapienie;
    readonly StringWriter bufor = new(new StringBuilder(1024), System.Globalization.CultureInfo.InvariantCulture);
    static readonly UTF8Encoding Utf8 = new(false);

    public OgraniczonyFormatter(ITextFormatter wewnetrzny, int maxBajtow, Action naObciecie, Action<LogEvent, string> naZastapienie)
    {
        this.wewnetrzny = wewnetrzny; this.maxBajtow = maxBajtow; this.naObciecie = naObciecie; this.naZastapienie = naZastapienie;
    }

    public void Format(LogEvent e, TextWriter output)
    {
        lock (bufor)
        {
            var linia = Formatuj(e);
            if (Bajty(linia) <= maxBajtow) { output.Write(linia); return; }
            var skrocony = Skroc(e);
            linia = Formatuj(skrocony);
            if (Bajty(linia) <= maxBajtow) { naObciecie?.Invoke(); output.Write(linia); return; }
            naZastapienie?.Invoke(e, "RECORD_TOO_LARGE");
            output.Write(Formatuj(Zastepczy(e)));
        }
    }

    string Formatuj(LogEvent e)
    {
        var sb = bufor.GetStringBuilder();
        sb.Clear();
        wewnetrzny.Format(e, bufor);
        return sb.ToString();
    }

    /// <summary>Rozmiar rekordu bez końca linii.</summary>
    static int Bajty(string linia) => Utf8.GetByteCount(linia.AsSpan().TrimEnd("\r\n"));

    static LogEvent Skroc(LogEvent e)
    {
        var props = e.Properties.Select(kv => new LogEventProperty(kv.Key,
            kv.Value is ScalarValue { Value: string s } && s.Length > 128 ? new ScalarValue(Redakcja.Przytnij(s, 128)) : kv.Value)).ToList();
        props.Add(new LogEventProperty("truncated", new ScalarValue(true)));
        var ex = e.Exception == null ? null : new WyjatekZredagowany(Redakcja.Przytnij(e.Exception.ToString(), 1024));
        return new LogEvent(e.Timestamp, e.Level, ex, e.MessageTemplate, props, e.TraceId ?? default, e.SpanId ?? default);
    }

    static readonly string[] Wymagane = { "schemaVersion", "stream", "severity", "moduleId", "moduleVersion", "processRunId", "recordSeq", "monoElapsedUs" };

    static LogEvent Zastepczy(LogEvent e)
    {
        var props = new List<LogEventProperty>();
        foreach (var n in Wymagane)
            if (e.Properties.TryGetValue(n, out var v)) props.Add(new LogEventProperty(n, v));
        props.Insert(2, new LogEventProperty("eventCode", new ScalarValue(ZdarzeniaLogow.ZaDuzy.Kod)));
        props.Insert(3, new LogEventProperty("eventId", new ScalarValue((long)ZdarzeniaLogow.ZaDuzy.Id)));
        if (e.Properties.TryGetValue("eventId", out var id)) props.Add(new LogEventProperty("originalEventId", id));
        props.Add(new LogEventProperty("reasonCode", new ScalarValue("RECORD_TOO_LARGE")));
        props.Add(new LogEventProperty("outcome", new ScalarValue("Degraded")));
        var t = new Serilog.Parsing.MessageTemplateParser().Parse("Rekord przekroczył limit rozmiaru i został zastąpiony");
        return new LogEvent(e.Timestamp, e.Level, null, t, props, e.TraceId ?? default, e.SpanId ?? default);
    }
}
