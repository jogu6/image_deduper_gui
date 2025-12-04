# Image Deduper (WinUI 3)

Image Deduper is a Windows 11-style WinUI 3 desktop app that wraps the proven Python workflow from `samplecodes/`, scans an image folder, and moves lower-quality duplicates to a `duplicates/` subfolder using SHA-1, perceptual hash, and SSIM.
Image Deduper は `samplecodes/` にある Python 版の手順を WinUI 3 で再構築した Windows 11 風デスクトップアプリで、画像フォルダーを走査し、SHA-1・pHash・SSIM を使って画質の劣る重複画像を `duplicates/` フォルダーに移動します。

## Download / ダウンロード

1. Open the repository’s **Releases** page on GitHub and pick the latest tag (for example `v1.0.0`).
2. Download `ImageDeduper-win-x64.zip` (or another RID-specific archive) from the Assets section.
3. Extract the ZIP. It contains `ImageDeduper.exe`, this README, and supporting files—place them anywhere and run the EXE directly.

1. GitHub の **Releases** ページで最新タグ（例: `v1.0.0`）を開きます。
2. Assets から `ImageDeduper-win-x64.zip`（または必要な RID の ZIP）を取得します。
3. ZIP を展開すると `ImageDeduper.exe` と README などが入っています。任意のフォルダーに置き、EXE を直接実行してください。

## Features / 特長

1. **Automatic format normalization**  
   Converts HEIC/HEIF (when the Microsoft HEIF Image Extensions are installed), renames `.jfif` to `.jpg`, and seamlessly handles WebP/BMP/GIF/TIFF/PNG/JPEG.  
   **自動フォーマット正規化** — Microsoft HEIF 画像拡張機能が入っていれば HEIC/HEIF も変換し、`.jfif` を `.jpg` にリネームし、WebP/BMP/GIF/TIFF/PNG/JPEG も透過的に扱います。

2. **Layered duplicate detection**  
   SHA-1 for exact matches, perceptual hash for clustering, SSIM for final confirmation. Only the lower-resolution (or older) file goes to `<source>/duplicates`.  
   **多層式重複検出** — 完全一致は SHA-1、類似候補は pHash、最終判定は SSIM で、低解像度または古い方のみ `<source>/duplicates` へ移動します。

3. **Resumable processing**  
   Separate checkpoints (cache load/comparison) stored beside the source folder so Stop/Start resumes exactly where you left off.  
   **再開機能** — 読み込みと比較それぞれにチェックポイントを用意し、フォルダー内に保存するため、Stop/Start で中断位置から再開できます。

4. **Modern compact UI**  
   Single page with one progress bar, live statistics, live logs with auto-scroll (unless manually scrolled), and a settings dialog.  
   **モダンかつコンパクトな UI** — 単一ページに進捗バー・統計・ライブログをまとめ、手動でスクロールしていなければ自動スクロールし、詳細な設定ダイアログも備えています。

5. **Localization & codec awareness**  
   English/Japanese strings reside in `ImageDeduper.Core/locales`. The settings dialog lists supported formats and shows a “Get from Microsoft Store” link if HEIF codecs are missing.  
   **多言語・コーデック検知** — 英語/日本語のリソースは `ImageDeduper.Core/locales` にあり、設定画面には対応フォーマットが表示され、HEIF コーデックが無い場合は「Microsoft Store から入手」リンクが出ます。

## Requirements / 必要環境

| Item / 項目 | Version / バージョン | Notes / 備考 |
| --- | --- | --- |
| .NET SDK | 8.0 | Needed only for building from source / ソースからビルドする場合に必要 |
| Windows App SDK | 1.8 (NuGet) | Restores automatically / NuGet で自動復元 |
| Windows 11 HEIF codec | Optional | Required to process HEIC/HEIF images / HEIC/HEIF を扱うには Microsoft Store 版を導入 |

## Build & Publish / ビルドと配布

```powershell
# English: build & run
# 日本語: ビルドと実行
dotnet build ImageDeduper.sln
dotnet run --project ImageDeduper.App

# Publish a single-file, self-contained exe (x64 example)
# 単体 self-contained EXE を生成する例 (x64)
dotnet publish ImageDeduper.App -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true
```

The final EXE is placed under `ImageDeduper.App/bin/Release/.../publish/ImageDeduper.exe`. Zip that folder (keeping this README inside) before uploading it as a Release asset.  
最終的な EXE は `ImageDeduper.App/bin/Release/.../publish/ImageDeduper.exe` に出力されます。ZIP 化する際は README も同梱し、GitHub Release の Assets にアップロードしてください。

## Usage Flow / 使い方

1. **Select folder / フォルダー選択** – Choose the image source; the UI shows the path and status.  
2. **Start / 開始** – `Start` を押すと読み込みフェーズ(Phase 1/2)→比較フェーズ(Phase 2/2)と進みます。  
3. **Monitor / 監視** – Statistics (Total / Processed / Moved / Elapsed) and live logs update continuously; logs stay copyable.  
4. **Stop & resume / 停止と再開** – `Stop` で中断すると次回 `Start` 時に「再開 / 最初から」を即時選択できます。  
5. **Duplicates folder / duplicates フォルダー** – 移動されたファイルは `<source>/duplicates` に集約され、ログで理由が分かります。

Settings (language, SSIM, pHash, codec hints) live in `setting.ini`, saved on exit, and ignored by git. Logs are stored next to the EXE. Resume checkpoints (`.loading.json`, `resume.json`) are created inside the source folder to keep runs self-contained.  
言語や SSIM・pHash・コーデック表示などの設定は `setting.ini` に保存され、終了時に更新されます（git では無視）。ログは EXE と同じ場所に出力され、再開用チェックポイント（`.loading.json`, `resume.json`）は処理対象フォルダー内に作成します。

## Localization / ローカライズ

- English resources: `ImageDeduper.Core/locales/en.json`  
- Japanese resources: `ImageDeduper.Core/locales/ja.json`  
Add new locale files and extend `AppSettings.Language` to support extra languages.  
新しい言語を追加する場合は JSON を追加し、`AppSettings.Language` を拡張してください。

## CLI Reference / 参考: CLI 版

The legacy Python CLI (still fully functional) resides in `samplecodes/README.md`. The WinUI application mirrors its logic while providing a GUI layer.  
旧 CLI 版（現在も動作）は `samplecodes/README.md` にあります。WinUI 版は同じロジックを GUI へ移植したものです。

## License / ライセンス

- Source code: MIT License (`LICENSE`).  
- Third-party dependencies: see [THIRD_PARTY_NOTICES.md](THIRD_PARTY_NOTICES.md).  

ソースコードは MIT License (`LICENSE`) です。  
サードパーティーライブラリについては [THIRD_PARTY_NOTICES.md](THIRD_PARTY_NOTICES.md) を参照してください。
