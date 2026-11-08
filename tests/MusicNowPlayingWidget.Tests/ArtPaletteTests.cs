using System.Drawing;
using System.Drawing.Imaging;
using MusicNowPlayingWidget.Media;
using Xunit;

public class ArtPaletteTests
{
    static byte[] Png(int w, int h, Action<Graphics> draw)
    {
        using var bmp = new Bitmap(w, h);
        using (var g = Graphics.FromImage(bmp)) draw(g);
        using var ms = new MemoryStream();
        bmp.Save(ms, ImageFormat.Png);
        return ms.ToArray();
    }

    static byte[] Solid(string hex) => Png(64, 64, g => g.Clear(ColorTranslator.FromHtml(hex)));

    static Color C(string hex) => ColorTranslator.FromHtml(hex);

    [Theory]
    [InlineData("#000000")]
    [InlineData("#FFFFFF")]
    [InlineData("#808080")]
    [InlineData("#1DB954")]
    [InlineData("#FFE600")]
    [InlineData("#0A1F6B")]
    public void From_SolidColors_MeetContrast(string hex)
    {
        var p = ArtPalette.From(Solid(hex));
        Assert.NotNull(p);
        Assert.True(ArtPalette.Contrast(p.Text, p.Background) >= 4.5, $"text {p.Text} on {p.Background}");
        Assert.True(ArtPalette.Contrast(p.Accent, p.Background) >= 3.0, $"accent {p.Accent} on {p.Background}");
    }

    [Fact]
    public void From_TwoColorImage_BackgroundIsLargerArea()
    {
        var p = ArtPalette.From(Png(100, 100, g =>
        {
            g.Clear(C("#14204A"));
            using var orange = new SolidBrush(C("#FF7A00"));
            g.FillRectangle(orange, 75, 0, 25, 100);
        }))!;
        var bg = C(p.Background);
        Assert.True(Math.Abs(bg.R - 0x14) <= 24 && Math.Abs(bg.G - 0x20) <= 24 && Math.Abs(bg.B - 0x4A) <= 24, p.Background);
        var accent = C(p.Accent);
        Assert.True(accent.R > accent.G && accent.G > accent.B, p.Accent);
    }

    [Fact]
    public void From_DarkBackground_WhiteText() => Assert.Equal("#FFFFFF", ArtPalette.From(Solid("#101010"))!.Text);

    [Fact]
    public void From_LightBackground_BlackText() => Assert.Equal("#000000", ArtPalette.From(Solid("#F0F0F0"))!.Text);

    [Fact]
    public void From_BadBytes_ReturnsNull()
    {
        Assert.Null(ArtPalette.From([1, 2, 3]));
        Assert.Null(ArtPalette.From([]));
    }

    [Fact]
    public void Contrast_KnownValues()
    {
        Assert.Equal(21, ArtPalette.Contrast("#000000", "#FFFFFF"), 2);
        Assert.Equal(21, ArtPalette.Contrast("#FFFFFF", "#000000"), 2);
    }
}
