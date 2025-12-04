---
layout: default
title: Image Deduper – Site Index
---

# Image Deduper – Site Index / サイト案内

[README](https://github.com/jogu6/image_deduper_gui/blob/main/README.md) | [Site Index](index.md) | [Privacy Policy](privacy.md) | [Terms of Use](terms.md)

## Overview / 概要
Image Deduper is a WinUI 3 desktop application for Windows 11 that scans a chosen folder, deduplicates images via SHA-1, perceptual hash, and SSIM, and moves the lower-quality copy into <source>/duplicates. It is the GUI successor to the CLI workflow hosted on GitHub.

Image Deduper は、選択したフォルダーを走査し、SHA-1・pHash・SSIM で画像の重複を検出して画質の劣る方を <source>/duplicates に移動する Windows 11 向け WinUI 3 アプリです。GitHub 上で公開されている CLI ワークフローを GUI に発展させたものです。

CLI reference implementation / CLI 版: https://github.com/jogu6/imagededuper

## Download / ダウンロード
1. Open the repository’s **Releases** page on GitHub and select the latest version (for example 1.0.0).  
   GitHub の **Releases** ページを開き、最新バージョン（例: 1.0.0）を選択します。
2. Download ImageDeduper-win-x64.zip (or the ZIP for your preferred RID) from the Assets list.  
   Assets から ImageDeduper-win-x64.zip など必要な RID の ZIP を取得します。
3. Extract the ZIP, keep ImageDeduper.exe, README, and notice files together, then run the EXE.  
   ZIP を展開し、ImageDeduper.exe と README・各種ノーティスを同じフォルダーに保ち、EXE を実行します。

## Feature Highlights / 主な機能
- **Format normalization / フォーマット正規化** – Handles JPEG/PNG/WebP/BMP/GIF/TIFF, renames .jfif, and converts HEIC/HEIF when Microsoft’s codec is installed.
- **Layered duplicate detection / 多層式重複検出** – Exact SHA-1 matches, perceptual hash clustering, SSIM confirmation, and automatic move to duplicates/.
- **Resume checkpoints / 再開チェックポイント** – Stop/Start resumes without reprocessing finished files.
- **Localization / 多言語対応** – English and Japanese UI strings with instant switching.
- **Settings insights / 設定詳細** – Dialog lists thresholds and supported formats, including a Microsoft Store link when HEIF codecs are missing.

## Additional Links / その他リンク
- Source repository root / ソースコード: https://github.com/jogu6/image_deduper_gui  
- CLI implementation / CLI 版: https://github.com/jogu6/imagededuper  
- Third-party notices / サードパーティー情報: [THIRD_PARTY_NOTICES.md](../THIRD_PARTY_NOTICES.md)
