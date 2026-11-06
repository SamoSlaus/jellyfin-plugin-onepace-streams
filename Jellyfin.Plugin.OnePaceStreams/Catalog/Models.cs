namespace Jellyfin.Plugin.OnePaceStreams.Catalog;

/// <summary>Audio/subtitle variant of a release.</summary>
public enum Language
{
    /// <summary>Japanese audio, English subtitles.</summary>
    Sub,

    /// <summary>English dub.</summary>
    Dub,

    /// <summary>English dub with closed captions / dubtitles.</summary>
    DubCC,
}

/// <summary>Edit of an episode.</summary>
public enum Cut
{
    /// <summary>Regular cut.</summary>
    Normal,

    /// <summary>Extended cut.</summary>
    Extended,

    /// <summary>Any other alternate cut (e.g. G-8 Alternate Cut).</summary>
    Alternate,
}

/// <summary>A single downloadable file for an episode.</summary>
public sealed record VideoSource(Language Language, Cut Cut, int Quality, string FileId);

/// <summary>A One Pace episode.</summary>
public sealed record Episode(
    int Number,
    string Title,
    string? Description,
    string? MangaChapters,
    Uri? ImageUrl,
    IReadOnlyList<VideoSource> Sources);

/// <summary>A One Pace arc (or special).</summary>
public sealed record Arc(
    string Slug,
    string Title,
    bool IsSpecial,
    string? MangaChapters,
    string? Description,
    Uri? ImageUrl,
    IReadOnlyList<Episode> Episodes);

/// <summary>The parsed catalog page.</summary>
public sealed record OnePaceCatalog(IReadOnlyList<Arc> Arcs, Uri? LogoUrl);
