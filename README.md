# One Pace Streams for Jellyfin

A Jellyfin plugin that adds [One Pace](https://onepace.net) to your library **without downloading anything**.
It reads the One Pace catalog and writes one small `.strm` file per episode that points to the
official Pixeldrain upload. Video is streamed from Pixeldrain only when you press play.

- Every arc becomes a season (Romance Dawn = Season 1, Orange Town = Season 2, …), specials go to Season 00.
- Titles, descriptions, covered manga chapters and artwork are written as `.nfo` / `.jpg` files, so no other metadata plugin is needed.
- Choose English Subtitles, English Dub or English Dub with Closed Captions, 480p/720p/1080p, and whether you prefer Extended Cuts.
- A daily scheduled task picks up new episodes and re-releases automatically.
- Watched state, resume and *Next Up* work like any other show.

> This project is not affiliated with One Pace or Pixeldrain. It does not host or redistribute any video;
> it only links to the files that One Pace publishes on its website.

## Requirements

- Jellyfin **12.2** or newer (Linux, Windows, Docker – any platform).
- The Jellyfin server must be able to reach `https://onepace.net` and `https://pixeldrain.net`.

## Installation

### Option A – Plugin repository (recommended)

1. In Jellyfin open **Dashboard → Plugins → Repositories** (in older layouts: *Plugins → Catalog → ⚙ Settings*).
2. Click **+** and add:
   - **Name:** `One Pace Streams`
   - **URL:** `https://raw.githubusercontent.com/SamoSlaus/jellyfin-plugin-onepace-streams/main/manifest.json`
3. Go to **Dashboard → Plugins → Catalog**, find **One Pace Streams** and click **Install**.
4. Restart Jellyfin.

Updates will then appear in the catalog automatically.

### Option B – Manual install

1. Download `onepace-streams_<version>.zip` from the [latest release](https://github.com/SamoSlaus/jellyfin-plugin-onepace-streams/releases/latest).
2. Extract it into a new folder inside Jellyfin's plugin directory:

   | Installation            | Plugin directory                                   |
   |-------------------------|----------------------------------------------------|
   | Debian/Ubuntu package   | `/var/lib/jellyfin/plugins/OnePaceStreams/`        |
   | Docker (official image) | `/config/plugins/OnePaceStreams/`                  |
   | Windows                 | `%ProgramData%\Jellyfin\Server\plugins\OnePaceStreams\` |

3. Make sure the Jellyfin user can read the files (Linux: `chown -R jellyfin:jellyfin <folder>`).
4. Restart Jellyfin.

## Setup

**Nothing to configure.** After installing and restarting Jellyfin, the plugin automatically:

1. syncs all episodes into `<Jellyfin data folder>/onepace` (e.g. `/var/lib/jellyfin/data/onepace`),
2. creates a **One Pace** Shows library for that folder (NFO reader on, online metadata fetchers off,
   chapter/trickplay image extraction off so scans never download whole videos),
3. scans it. This takes a couple of minutes on first run.

The sync then runs at every server start and daily at 04:00 (change it in **Dashboard → Scheduled Tasks → One Pace**).

### Settings (optional)

**Dashboard → Plugins → One Pace Streams**:

| Setting | Description |
|---------|-------------|
| Output folder | Where the files are written. Empty = `<Jellyfin data folder>/onepace`. Docker users may prefer a mounted volume, e.g. `/media/onepace`. |
| Language | English Subtitles, English Dub, or English Dub with Closed Captions. |
| Quality | Preferred resolution. If an episode isn't available in it, the next lower one is used, otherwise the lowest higher one. |
| Prefer Extended Cut | Use the Extended Cut for episodes that have one. |
| Use another language when the preferred one is missing | E.g. a Dub user still gets the subbed version of not-yet-dubbed arcs. |
| Include specials | Write specials to *Season 00*. |
| Download arc and episode images | Saves the series poster, logo and backdrop plus season and episode thumbnails next to the files. |
| Create a "One Pace" library on the next sync | On by default for new installs; turns itself off after running. Skipped if a library already contains the output folder. Tick it again if you deleted the library or changed the output folder. |

After changing settings, run **Dashboard → Scheduled Tasks → One Pace → Sync One Pace** to apply them immediately.

## What gets written

```
/media/onepace/
├── .onepace-manifest.json          ← list of files the plugin owns
└── One Pace/
    ├── tvshow.nfo
    ├── poster.jpg                  ← generated: first arc image + One Pace logo
    ├── backdrop.jpg
    ├── logo.png
    ├── Season 00/                  ← specials
    ├── Season 01/                  ← Romance Dawn
    │   ├── season.nfo
    │   ├── folder.jpg
    │   ├── One Pace - S01E01.strm  ← https://pixeldrain.net/api/file/<id>
    │   ├── One Pace - S01E01.nfo
    │   ├── One Pace - S01E01-thumb.jpg
    │   └── …
    └── Season 02/                  ← Orange Town
```

- File names are stable (`SxxEyy`), so changing language/quality or a re-titled episode keeps your watch history.
- Files are only rewritten when their content changes.
- The plugin only deletes files listed in its manifest; anything you put in the folder yourself is never touched.
- If the catalog suddenly returns no episodes or shrinks by more than half (e.g. the website is down or changed), nothing is deleted.

## Known limitations

- **Pixeldrain free tier** has a daily bandwidth limit per IP and may ask for a captcha when it is exceeded. A Pixeldrain subscription removes this but is not used by the plugin yet.
- During library scans Jellyfin briefly reads the start of each remote file.
- When One Pace re-releases an episode, the `.strm` file is updated automatically; Jellyfin may treat it as changed media.
- The catalog is read from the One Pace website's HTML. If the site's layout changes, the sync may fail until the plugin is updated – your existing library is kept in that case.
- If arcs are ever inserted in the middle of the One Pace timeline, later season numbers shift.
- Only the English variants offered on onepace.net are supported.

### Using it together with the old "One Pace" metadata plugin

The jwueller *One Pace* plugin (10.9.x) is not needed. If it is installed, disable its metadata and image
fetchers for this library (or uninstall it), otherwise it may overwrite the titles from the NFO files.

## Building from source

Requires the [.NET 10 SDK](https://dotnet.microsoft.com/download).

```sh
dotnet test
dotnet publish Jellyfin.Plugin.OnePaceStreams -c Release -o publish
```

Copy `publish/Jellyfin.Plugin.OnePaceStreams.dll` and `publish/AngleSharp.dll` into the plugin folder as described in *Manual install*.

To additionally check the parser against the live website, save https://onepace.net/en/watch and run
`ONEPACE_WATCH_HTML=/path/to/watch.html dotnet test`.

### Releasing

Push a tag like `v1.0.1.0`. GitHub Actions runs the tests, builds the zip, creates the GitHub release and
adds the new version to `manifest.json`.

## License

[GPL-3.0](LICENSE)
