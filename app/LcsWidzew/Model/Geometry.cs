namespace LcsWidzew.Model;

/// <summary>Punkt rysunku układu lub zobrazowania. Wartość bez zależności od interfejsu graficznego.</summary>
public readonly record struct Point2(double X, double Y);

/// <summary>Prostokąt osiowy wyznaczony przez dwa przeciwległe narożniki.</summary>
public readonly record struct Bounds2
{
    public double X { get; }
    public double Y { get; }
    public double Width { get; }
    public double Height { get; }
    public double Left => X;
    public double Top => Y;
    public double Right => X + Width;
    public double Bottom => Y + Height;

    public Bounds2(Point2 first, Point2 second)
    {
        X = Math.Min(first.X, second.X); Y = Math.Min(first.Y, second.Y);
        Width = Math.Max(first.X, second.X) - X; Height = Math.Max(first.Y, second.Y) - Y;
    }

    public bool Contains(Point2 point) => point.X >= Left && point.X <= Right && point.Y >= Top && point.Y <= Bottom;
}
