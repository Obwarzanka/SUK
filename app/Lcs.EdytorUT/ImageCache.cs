using System.IO;
using System.Windows;
using System.Windows.Media.Imaging;

namespace Lcs.EdytorUT;

/// <summary>
/// Obrazy podkładów w pamięci, wczytywane w tle na kilku poziomach szczegółowości (poziom n – szerokość W / 2ⁿ).
/// Przy oddaleniu duże zdjęcia zajmują mało pamięci; przy przybliżeniu wczytuje się pełna rozdzielczość tylko widocznych kafelków.
/// Najdawniej używane obrazy są zwalniane powyżej limitu pamięci.
/// </summary>
public class ImageCache
{
    record Entry(BitmapSource Bmp, long Bytes) { public long Used; }

    readonly Dictionary<(string File, int Level), Entry> map = new();
    readonly HashSet<(string, int)> pending = new();
    readonly HashSet<string> missing = new(StringComparer.OrdinalIgnoreCase);
    readonly SemaphoreSlim slots = new(3);
    long bytes, tick;
    public long LimitBytes = 900L << 20;
    /// <summary>Obraz wczytany (na wątku okna).</summary>
    public event Action Loaded;
    public int PendingCount { get { lock (map) return pending.Count; } }

    public bool IsMissing(string file) { lock (map) return missing.Contains(file); }

    /// <summary>Obraz na żądanym poziomie albo – do czasu wczytania – najbliższy dostępny; null – jeszcze nic.</summary>
    public BitmapSource Get(string file, int level, int fullWidth)
    {
        lock (map)
        {
            if (map.TryGetValue((file, level), out var e)) { e.Used = ++tick; return e.Bmp; }
            if (!missing.Contains(file) && pending.Add((file, level))) _ = Load(file, level, fullWidth);
            // zastępczo: najpierw dokładniejszy, potem mniej dokładny
            for (int d = 1; d <= 8; d++)
            {
                if (level - d >= 0 && map.TryGetValue((file, level - d), out var f)) { f.Used = ++tick; return f.Bmp; }
                if (map.TryGetValue((file, level + d), out var g)) { g.Used = ++tick; return g.Bmp; }
            }
            return null;
        }
    }

    async Task Load(string file, int level, int fullWidth)
    {
        await slots.WaitAsync();
        try
        {
            var bmp = await Task.Run(() =>
            {
                if (!File.Exists(file)) return null;
                var bi = new BitmapImage();
                bi.BeginInit(); bi.CacheOption = BitmapCacheOption.OnLoad; bi.UriSource = new Uri(file);
                bi.CreateOptions = BitmapCreateOptions.IgnoreColorProfile;
                if (level > 0) bi.DecodePixelWidth = Math.Max(1, fullWidth >> level);
                bi.EndInit(); bi.Freeze();
                return (BitmapSource)bi;
            });
            lock (map)
            {
                pending.Remove((file, level));
                if (bmp == null) missing.Add(file);
                else
                {
                    long b = (long)bmp.PixelWidth * bmp.PixelHeight * 4;
                    map[(file, level)] = new Entry(bmp, b) { Used = ++tick }; bytes += b;
                    Trim();
                }
            }
        }
        catch { lock (map) { pending.Remove((file, level)); missing.Add(file); } }
        finally { slots.Release(); }
        Application.Current?.Dispatcher.BeginInvoke(() => Loaded?.Invoke());
    }

    void Trim()
    {
        while (bytes > LimitBytes && map.Count > 1)
        {
            var old = map.MinBy(kv => kv.Value.Used);
            map.Remove(old.Key); bytes -= old.Value.Bytes;
        }
    }

    /// <summary>Plik zmieniony albo pojawił się – wczytaj od nowa.</summary>
    public void Forget(string file)
    {
        lock (map)
        {
            missing.Remove(file);
            foreach (var k in map.Keys.Where(k => string.Equals(k.File, file, StringComparison.OrdinalIgnoreCase)).ToList()) { bytes -= map[k].Bytes; map.Remove(k); }
        }
    }
}
