using Jellyfin.Plugin.OnePaceStreams.Sync;
using SkiaSharp;

namespace Jellyfin.Plugin.OnePaceStreams.Tests;

public class PosterComposerTests
{
    [Fact]
    public void ComposesPortraitJpegFromLandscapeBackdrop()
    {
        var backdrop = SolidImage(1920, 1080, SKColors.SkyBlue, SKEncodedImageFormat.Jpeg);
        var logo = SolidImage(600, 200, SKColors.Red, SKEncodedImageFormat.Png);

        using var poster = SKBitmap.Decode(PosterComposer.Compose(backdrop, logo));

        Assert.Equal(PosterComposer.Width, poster.Width);
        Assert.Equal(PosterComposer.Height, poster.Height);
        var logoPixel = poster.GetPixel(PosterComposer.Width / 2, PosterComposer.Height - 250);
        Assert.True(logoPixel.Red > 200 && logoPixel.Blue < 80, $"Expected the logo near the bottom, got {logoPixel}");
    }

    [Fact]
    public void RejectsInvalidImages()
    {
        Assert.Throws<InvalidDataException>(() => PosterComposer.Compose([1, 2, 3], [4, 5, 6]));
    }

    private static byte[] SolidImage(int width, int height, SKColor color, SKEncodedImageFormat format)
    {
        using var bitmap = new SKBitmap(width, height);
        bitmap.Erase(color);
        using var image = SKImage.FromBitmap(bitmap);
        using var data = image.Encode(format, 95);
        return data.ToArray();
    }
}
