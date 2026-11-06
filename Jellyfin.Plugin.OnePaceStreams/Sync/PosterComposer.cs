using SkiaSharp;

namespace Jellyfin.Plugin.OnePaceStreams.Sync;

/// <summary>Creates a portrait series poster from a landscape backdrop and the logo.</summary>
public static class PosterComposer
{
    /// <summary>Poster width in pixels.</summary>
    public const int Width = 1000;

    /// <summary>Poster height in pixels.</summary>
    public const int Height = 1500;

    /// <summary>Composes the poster.</summary>
    /// <param name="backdrop">Encoded backdrop image.</param>
    /// <param name="logo">Encoded logo image (ideally with transparency).</param>
    /// <returns>JPEG bytes.</returns>
    public static byte[] Compose(byte[] backdrop, byte[] logo)
    {
        using var background = SKImage.FromEncodedData(backdrop) ?? throw new InvalidDataException("Unsupported backdrop image.");
        using var logoImage = SKImage.FromEncodedData(logo) ?? throw new InvalidDataException("Unsupported logo image.");
        using var surface = SKSurface.Create(new SKImageInfo(Width, Height)) ?? throw new InvalidDataException("Could not create drawing surface.");
        var canvas = surface.Canvas;
        var sampling = new SKSamplingOptions(SKCubicResampler.Mitchell);

        canvas.Clear(SKColors.Black);
        var scale = Math.Max((float)Width / background.Width, (float)Height / background.Height);
        var drawWidth = background.Width * scale;
        var drawHeight = background.Height * scale;
        canvas.DrawImage(background, SKRect.Create((Width - drawWidth) / 2, (Height - drawHeight) / 2, drawWidth, drawHeight), sampling);

        using (var shade = new SKPaint())
        {
            shade.Shader = SKShader.CreateLinearGradient(
                new SKPoint(0, Height * 0.4f),
                new SKPoint(0, Height),
                [SKColors.Transparent, new SKColor(0, 0, 0, 220)],
                SKShaderTileMode.Clamp);
            canvas.DrawRect(0, 0, Width, Height, shade);
        }

        var logoWidth = Width * 0.86f;
        var logoHeight = logoImage.Height * logoWidth / logoImage.Width;
        canvas.DrawImage(logoImage, SKRect.Create((Width - logoWidth) / 2, Height - logoHeight - (Height * 0.08f), logoWidth, logoHeight), sampling);

        using var snapshot = surface.Snapshot();
        using var data = snapshot.Encode(SKEncodedImageFormat.Jpeg, 90);
        return data.ToArray();
    }
}
