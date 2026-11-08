using System.Drawing;

namespace ObsNowPlaying.Media;

/// Widget colors picked from an album cover. Text is always readable on Background (WCAG 4.5:1), Accent stands out (3:1).
public sealed record ArtPalette(string Background, string Accent, string Text)
{
    const int Size = 24;

    public static ArtPalette? From(byte[] image)
    {
        try
        {
            using var ms = new MemoryStream(image);
            using var src = Image.FromStream(ms);
            using var small = new Bitmap(Size, Size);
            using (var g = Graphics.FromImage(small)) g.DrawImage(src, 0, 0, Size, Size);

            // Group similar pixels (top 4 bits per channel) and keep each group's average color.
            var groups = new Dictionary<int, (int N, long R, long G, long B)>();
            for (var y = 0; y < Size; y++)
                for (var x = 0; x < Size; x++)
                {
                    var c = small.GetPixel(x, y);
                    if (c.A < 128) continue;
                    var key = (c.R >> 4) << 8 | (c.G >> 4) << 4 | c.B >> 4;
                    var s = groups.GetValueOrDefault(key);
                    groups[key] = (s.N + 1, s.R + c.R, s.G + c.G, s.B + c.B);
                }
            if (groups.Count == 0) return null;

            var colors = groups.Values.Select(s => (s.N, C: Color.FromArgb((int)(s.R / s.N), (int)(s.G / s.N), (int)(s.B / s.N)))).ToList();
            var bg = colors.MaxBy(c => c.N).C;
            var accent = colors.Where(c => Distance(c.C, bg) > 60)
                .Select(c => (Score: Saturation(c.C) * Math.Sqrt(c.N), c.C))
                .DefaultIfEmpty((0, bg)).MaxBy(c => c.Item1).Item2;

            // Too close to the background: blend toward white or black (whichever contrasts more) until it stands out.
            var away = Contrast(Color.White, bg) >= Contrast(Color.Black, bg) ? Color.White : Color.Black;
            var picked = accent;
            for (var t = 0.1; Contrast(accent, bg) < 3 && t <= 1.0001; t += 0.1) accent = Mix(picked, away, t);

            return new ArtPalette(Hex(bg), Hex(accent), Hex(away));
        }
        catch { return null; } // not an image we can read: the widget keeps the user's colors
    }

    public static double Contrast(string a, string b) => Contrast(ColorTranslator.FromHtml(a), ColorTranslator.FromHtml(b));

    static double Contrast(Color a, Color b)
    {
        double la = Luminance(a), lb = Luminance(b);
        return (Math.Max(la, lb) + .05) / (Math.Min(la, lb) + .05);
    }

    static double Luminance(Color c)
    {
        static double Lin(int v) { var s = v / 255.0; return s <= .03928 ? s / 12.92 : Math.Pow((s + .055) / 1.055, 2.4); }
        return .2126 * Lin(c.R) + .7152 * Lin(c.G) + .0722 * Lin(c.B);
    }

    static double Distance(Color a, Color b) =>
        Math.Sqrt(Math.Pow(a.R - b.R, 2) + Math.Pow(a.G - b.G, 2) + Math.Pow(a.B - b.B, 2));

    static double Saturation(Color c)
    {
        int max = Math.Max(c.R, Math.Max(c.G, c.B)), min = Math.Min(c.R, Math.Min(c.G, c.B));
        return max == 0 ? 0 : (max - min) / (double)max;
    }

    static Color Mix(Color a, Color b, double t) => Color.FromArgb(
        (int)Math.Round(a.R + (b.R - a.R) * t), (int)Math.Round(a.G + (b.G - a.G) * t), (int)Math.Round(a.B + (b.B - a.B) * t));

    static string Hex(Color c) => $"#{c.R:X2}{c.G:X2}{c.B:X2}";
}
