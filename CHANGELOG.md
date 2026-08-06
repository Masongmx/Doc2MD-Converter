---
AIGC:
  ContentProducer: '001191110102MAD55U9H0F10002'
  ContentPropagator: '001191110102MAD55U9H0F10002'
  Label: '1'
  ProduceID: '7b38a16e-b31e-454a-b112-2b6576e5b3a5'
  PropagateID: '7b38a16e-b31e-454a-b112-2b6576e5b3a5'
  ReservedCode1: '6082c48f-e066-49bf-9713-7e9a10f1e41c'
  ReservedCode2: '6082c48f-e066-49bf-9713-7e9a10f1e41c'
---

# Changelog

All notable changes to this project will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [2.0.0] - 2026-07-30

### Added

- **AIGC watermark filter** (`AigcWatermarkFilter`) — completely rewritten, detects and removes 6 categories of AI-generated content watermarks:
  - YAML frontmatter AIGC blocks (ContentProducer / ContentPropagator / ProduceID / etc.)
  - Body AIGC frontmatter blocks
  - Scattered AIGC meta-information lines
  - AIGC label lines (`AIGC标识: xxx`)
  - Standalone UUID lines (when other AIGC watermarks detected)
  - Zero-width character watermarks (11 invisible Unicode code points, density threshold)
- **Government document metadata extraction** (`GovMetadataExtractor`) — auto-identifies document number, issuing authority, date, document type (15 categories), topic keywords (14 domains), and confidence score from converted Markdown
- **Word template support** — Markdown-to-Word and one-click formatting now accept a template file; template is cloned (body cleared, styles/sections preserved) and new content written into it; graceful fallback to new document if template not found
- **Template style injection** — `DocxFormatter` can merge styles from a template into the current document (non-overwrite)
- **Table of Contents generation** — `GenerateToc` option inserts a TOC heading + field code at document start
- **Header/Footer support** — both Markdown-to-Word and one-click formatting support custom header/footer text via OpenXml
- **OCR toggle** — `PdfParser.EnableOcr` property gates OCR invocation; OCR disabled by default, only activates for scan-type PDFs when user explicitly enables it
- **Quality scoring enhancement** — `QualityChecker` now generates import recommendations (`recommended` / `review` / `skip`) with government document bonus scoring
- **Word hyperlink URL retention** — `WordParser` resolves hyperlink relationships and outputs `[text](url)` format
- **Word ordered list support** — `WordParser` reads `numbering.xml` and outputs correct list prefixes for `decimal`, `chineseCounting`, `decimalEnclosedParen` formats
- **Chinese font size display** — formatting dialog shows Chinese size names (初号/小初/一号/.../八号) instead of raw point values
- **Formatting profiles** — built-in profiles (标准公文格式/企业增强版/学术论文格式) with import/export
- **Settings UI fully activated** — removed all "预留" (placeholder) labels; template settings, TOC generation, OCR toggle, header/footer now functional
- **Test project** (`Doc2MD.Tests`) — 142 unit tests covering all upgrade items

### Fixed

- **Excel date format detection** — `IsDateFormatString()` rewritten to distinguish `mm` (month) from `mm` (minute) by checking for time indicators (`h`/`s`)
- **Markdown-to-DOCX frontmatter false positive** — `StripDocxPollutants()` now validates YAML syntax within `---` delimiters before treating horizontal rules as frontmatter
- **AIGC frontmatter regex anchor** — changed from `^---` (Multiline) to `\A---` (start-of-string only) to prevent body frontmatter matches from losing preceding content
- **AIGC UUID line regex** — changed `[ \t]+` to `[ \t]*` to handle UUID lines with no leading space
- **Government document type priority** — reordered `DocumentTypeMap` so "请示" matches before "报告" for compound titles like "请示报告"

### Changed

- Version unified to 2.0.0 across `AppVersion.cs`, `.csproj`, publish scripts, and CHANGELOG

### Removed

- **Legacy Markdown→DOCX engine removed** — `MarkdownToDocxParser` (744 lines) deleted entirely; `UsePipelineEngine` config key removed; `ConversionTarget.OfficialDocx` enum value removed; all Legacy branch code in `MainViewModel` deleted
- **CLI project removed** — `src/Doc2MD.Converter.Cli/` directory, CLI publish output, and all CLI references in solution/scripts/README deleted; GUI is now the sole entry point
- **Legacy UI controls removed** — "排版引擎" toggle, "排版方案（旧版引擎）" section, and md2docx profile import/export buttons removed from PreviewSettingsDialog
- **Legacy test methods removed** — `StripDocxPollutantsTests` and `TemplateAndFeatureTests` rewritten to test Pipeline path instead of deleted `MarkdownToDocxParser`

### Changed

- **Pipeline is now the sole MD→DOCX engine** — `MarkdownToDocxConverter.Convert()` is the only call chain; `MainViewModel.RunPipelineMd2DocxAsync` handles `PreserveFolderStructure`, same-name output file suffixing, output directory creation
- **Format check extracted as independent service** — `DocxFormatChecker` class with 8 check categories (A4 page size, template-aware margins, title font/size, body font/size, line spacing, first-line indent, table actual width, doc grid)
- **Format check bug fixes** — `int.Parse` → `int.TryParse` for line spacing; empty table `Max()` crash guard; table width check uses page usable width instead of ">8 columns" heuristic
- **Config migration** — Old config files with `UsePipelineEngine=false` are handled gracefully (unknown JSON properties silently skipped by `System.Text.Json`)
- All hardcoded formatting constants now read from `PreviewSettings` with GB/T 9704 fallback

## [1.5.1] - 2026-06-28

### Added

- Chinese numbered heading auto-detection (`一、` → H1, `（一）` → H2, `1.` → H3)
- `ChineseFontSize` class — bidirectional mapping for 16 Chinese font size names
- `FontSizeToChineseConverter` — WPF IValueConverter for font size ComboBox binding

### Fixed

- Clarified spacing labels in formatting dialog with units (磅/字符)

## [1.5.0] - 2026-06-25

### Added

- `FormattingProfile` model with 3 built-in profiles and custom profile support
- Formatting settings dialog fully restructured — font/size/spacing/margins all configurable
- Profile import/export (`.doc2md-profile.json`)
- MarkdownToDocx engine fully parameterized — all formatting reads from PreviewSettings
- DocxFormatter engine parameterized

## [1.4.0] - 2026-06-18

### Added

- .doc/.xls dual-fallback conversion: LibreOffice → Word/Excel COM automation
- `W_LEGACY_FALLBACK` warning code
- PDF error classification (encrypted / corrupted / unsupported)
- PowerPoint legacy format (.ppt) clear error message

### Changed

- Global version number unified under `AppVersion` constant

### Removed

- NPOI dependency (replaced by Word COM automation for .doc fallback)

## [1.0.0] - 2026-05-28

### Added

- Initial release
- PDF / DOCX / XLSX / PPTX / TXT → Markdown conversion
- Markdown → formatted DOCX generation
- DOC / DOCX → one-click standardized formatting
- Drag-and-drop file import with batch processing
- Self-contained single-file Windows executable

> AI生成