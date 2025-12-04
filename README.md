# Image Deduper (WinUI 3)

`ImageDeduper.App` is a Windows 11–style desktop UI that wraps the Python CLI logic from `samplecodes/`. It scans image folders, normalizes formats (HEIC/JFIF included), and moves lower-quality duplicates into a `duplicates/` subfolder. Only image content (SHA-1, pHash, SSIM) is compared – file names, timestamps, and metadata are ignored.

## Features

1. **Automatic format normalization**
   - Converts HEIC/HEIF → JPEG when the Windows *HEIF 画像表示オプション / HEIF Image Extensions* are installed.
   - Renames `.jfif` to `.jpg`, fixes mismatched extensions, handles WebP/BMP/GIF/TIFF transparently.

2. **Layered duplicate detection**
   - SHA-1 for exact matches, pHash to shortlist candidates, SSIM for final confirmation.
   - Moves only the lower-resolution (or older) file into `<source>/duplicates`.

3. **Resumable processing**
   - Separate checkpoints for cache loading (`.loading.json`) and comparison (`resume.json`) stored beside the source folder.
   - Stop / Start can resume exactly where the run halted.

4. **Modern WinUI experience**
   - Single compact dashboard, single progress bar showing `フェーズ 1/2` → `フェーズ 2/2`.
   - Live logs pane with auto-scroll (unless manually scrolled), copyable text.
   - Settings dialog with SSIM/pHash/language, plus a scrollable list of supported formats. HEIC entries show a “Get from Microsoft Store” link when codecs are missing.

5. **Localization**
   - English (`en`) and Japanese (`ja`) strings live in `ImageDeduper.Core/locales/*.json`. Switching language is instant once processing is idle.

## Requirements

| Item | Version | Notes |
| --- | --- | --- |
| .NET SDK | 8.0 | `dotnet --list-sdks` |
| Windows App SDK | 1.8 (packaged via NuGet) | Restored automatically |
| Windows 11 codecs | Optional but recommended | Install “HEIF 画像表示オプション / HEIF Image Extensions” from Microsoft Store to process HEIC/HEIF images |

## Build & Run

```powershell
# Restore + build both projects
dotnet build ImageDeduper.sln

# Run the WinUI app for development
dotnet run --project ImageDeduper.App

# Publish a self-contained exe (example: x64)
dotnet publish ImageDeduper.App -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true
```

The app persists window size, thresholds, and language in `setting.ini` (ignored by git). Log files land under `log/` relative to the EXE. Resume/cache files are written in the selected source folder.

## Usage Flow

1. **Select folder** – Choose a folder that contains images. The UI shows the path and current status (“Select folder”, “Processing…”, etc.).
2. **Start processing** – The Start button kicks off the pipeline. Progress bar shows `読み込み 1/2` while cache builds, then `比較 2/2` when pairwise comparisons begin.
3. **Live monitoring** – Statistics (Total images / Processed / Moved / Elapsed) update continuously. Logs in the right pane stream events and warnings (e.g., HEIC codec missing).
4. **Stop / resume** – Press Stop to pause; the next Start will prompt whether to resume or restart. Checkpoints are cleared automatically on success.
5. **Duplicates** – Moved files appear in `<source>/duplicates`. The log details which files were moved and why (SHA match or SSIM score).

## Supported Formats

| Format | Extensions | Status |
| --- | --- | --- |
| JPEG | `.jpg, .jpeg` | Native support |
| PNG | `.png` | Native support |
| BMP | `.bmp` | Native support |
| GIF | `.gif` (first frame) | Native support |
| TIFF | `.tiff, .tif` | Native support |
| WebP | `.webp` | Native support |
| JFIF | `.jfif` | Auto-renamed to `.jpg` |
| HEIC / HEIF | `.heic, .heif` | Requires HEIF codec; app shows a Microsoft Store link when unavailable |

## Localization & Customization

- Change language via Settings dialog (default `en`). Strings live in `ImageDeduper.Core/locales/en.json` and `locales/ja.json`. Add another JSON file + update `AppSettings.Language` if more languages are needed.
- SSIM/pHash thresholds can be adjusted live when idle; default SSIM is `0.85`, pHash Hamming threshold `40`.

## CLI Reference

The original Python CLI lives in `samplecodes/` and includes `README.md`, `config.py`, and the legacy core logic. The WinUI app mirrors its behavior but provides a modern Windows experience.

## License

MIT License. See `LICENSE`. Dependencies retain their original licenses (ImageSharp, Microsoft.WindowsAppSDK, etc.).
