# Changelog

All notable changes to this project will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [Unreleased]

## [1.0.0] - 2026-08-12

### Fixed

- **发布版启动失败（XamlParseException）**：`Styles.xaml` 中 `ToastActionButtonStyle` 以 `BasedOn="{StaticResource GhostButtonStyle}"` 前向引用在后方定义的样式，WPF StaticResource 不支持前向引用，导致 `MainWindow` BAML 加载时抛「无法找到名为 GhostButtonStyle 的资源」异常、应用启动即退出；已调整定义顺序将 `ToastActionButtonStyle` 移至 `GhostButtonStyle` 之后
- **日志缺失内部异常链**：`FileLoggingService` 改为记录完整 `Exception.ToString()`（含 InnerException 与堆栈），便于排查 XAML 解析等深层错误
- **老用户误弹首次引导浮层（标题栏变黑 + 设置/帮助按钮失效）**：旧版配置文件无 `HasCompletedOnboarding` 字段，反序列化后恒为 `false`，导致每次启动都显示全屏引导遮罩（80% 黑色），遮罩拦截整窗点击使标题栏按钮失效；`ConfigService` 新增旧配置升级逻辑，已有配置文件的用户直接视为已完成引导；引导卡片同时增加「跳过」按钮兜底，避免将来再次卡住界面

### Added

- **DI 容器与日志抽象**（C1）：引入 `Microsoft.Extensions.DependencyInjection`，新增 `ILoggingService` 接口与 `FileLoggingService` 实例实现；`LoggingService` 改为门面模式（`SetLogger` 可替换底层实现，兼容既有静态调用）；`App` 层通过 `ServiceCollectionExtensions` 注册 `ILoggingService` / `ConfigService` / `IParserRegistry` / `ConversionService` / `MainViewModel`；`ConversionService` 与 `MainViewModel` 支持构造函数注入 `ILoggingService`，App 启动改为从容器解析主窗口与 ViewModel
- **日志服务单元测试**（7 个）：覆盖 `FileLoggingService` 写入/格式化/日期文件名与 `LoggingService` 门面 `SetLogger` 可替换性与 null 防护

### Changed

- `MainWindow.xaml` 移除 XAML 内嵌 `<vm:MainViewModel/>` 实例化与 `App.xaml` 的 `StartupUri`，改为 `App.OnStartup` 从 DI 容器解析并设置 `DataContext`
- **统一 `DocxFormattingOptions`**（C2）：删除 Pipeline 命名空间下与 `Doc2MD.Models` 中同名的重复类，统一为单一模型（保留多级标题/页码/文档网格/字间距等 Pipeline 属性，合并 FormatDoc 侧 Markdown 标题/代码块字体属性），`DocxTemplate`/`DocxRenderer`/`StyleApplier` 改用 `Doc2MD.Models` 引用；内置"企业增强版"方案改为复用 `EnterpriseEnhanced()` 工厂方法，与 Pipeline 企业增强版模板参数完全一致；补充 9 个单元测试覆盖工厂方法、缩进计算、JSON 往返与设置双向映射
- **MainWindow 构造函数注入 ViewModel**（C7）：移除 `(MainViewModel)DataContext` 强制转换，改为构造函数注入（App 启动时从 DI 容器解析）
- **模板占位方法降级**（C9）：`TemplateRepository` 的 `SaveTemplate`/`DeleteTemplate`/`CloneTemplate` 不再抛 `NotImplementedException`，改为记录 Warning 日志并降级返回，避免调用方崩溃
- **Windows 保留名防护**（S2）：`SecurityPolicyService.SanitizeFileName` 对 CON/PRN/AUX/NUL/COM1-9/LPT1-9 等保留设备名添加下划线前缀（含带扩展名场景）
- **补充安全策略与模板占位测试**（28 个）：`SecurityPolicyServiceTests`（路径允许/隔离/覆盖保护/类型与大小限制/保留名净化）+ `TemplateRepository` 降级与内置模板解析测试
- **模板样式注入失败日志**（C6）：`DocxFormatter.InjectTemplateStyles` 空 catch 补充 Warning 日志，便于排查
- **拆分 `ConversionResult`**（C4）：God Object 拆分为 `ConversionResult`（核心输出：Success/OutputPath/Error/时长/输出文件/图片表格导出）+ `ConversionMetadata`（来源信息/文档统计/公文元数据）+ `ConversionQuality`（警告/质量评分/导入建议），全部 Parser 与后处理服务改用子对象访问，消除 30+ 字段的单类
- **拆分 `SemanticDocumentConverter.Convert()`**（C5）：约 200 行的主循环拆分为 `TryParseHeading`/`TryParseTable`/`TryParseUnorderedList`/`TryParseOrderedList`/`TryParseHorizontalRule`/`TryParseBlockquote` 及 `FlushParagraph`/`SkipPreamble`/`SkipHtmlComment`，编译期正则表达式提取为静态只读字段，行为完全不变（275 测试全绿）

### Added

- **转换预览**（F2）：MD→DOCX 模式新增「预览」按钮与右侧预览面板，复用 `SemanticDocumentConverter` 语义解析管线渲染为 WPF FlowDocument（标题/段落/表格/列表/引用/代码块/行内格式），颜色引用应用级画刷自动适配深浅主题；面板与上传区域互斥切换，切换模式或关闭时自动隐藏，支持文件列表单选联动实时刷新（零新增 NuGet 依赖）
- **转换历史记录**（F4）：新增 `ConversionRecord` 模型，`ConfigService.RememberConversion` 将最近 20 条转换记录（时间/源文件/输出路径/结果/质量评分/模式）持久化到 `RecentState` 配置；`ConversionService.ConvertFileAsync` 改为返回 `Task<ConversionResult?>`，`MainViewModel` 在转换完成或失败终态统一调用 `RecordConversion` 写入历史（含排版与 MD→DOCX 模式）
- **快捷键支持**（F5）：`MainWindow.OnPreviewKeyDown` 实现全局快捷键——Ctrl+O 添加文件、Ctrl+Shift+O 添加文件夹、F5 刷新当前文件夹、Ctrl+Enter 开始转换、Esc 取消处理、Ctrl+Z 撤销清空列表（`MainViewModel.UndoClearFiles` 基于清空快照恢复，含 `Toast_NothingToUndo`/`Toast_ListRestored` 等新资源）；帮助对话框「键盘快捷键」区块同步列出全部快捷键
- **自动更新检查**（F6）：新增 `UpdateService`（Core 层）轮询 GitHub Releases `releases/latest` API，解析 `tag_name` 与 `html_url`/`browser_download_url` 资产，版本 tag 解析/规范化/新旧比较逻辑公开为 `TryParseVersion`/`NormalizeTag`/`IsNewerVersion` 便于测试；`MainViewModel.CheckForUpdates` 改为异步检查，无新版本提示"已是最新"，有新版本弹出对话框询问并跳转下载（优先 exe/msix/zip 直链，其次 Release 页）；设置页「关于软件」区块启用「检查更新」按钮；新增 20 个单元测试覆盖 tag 解析与版本比较
- **清空列表可撤销**（P4）：清空文件列表不再弹阻塞式 MessageBox，改为 Toast 提示「已清空 N 个文件，可撤销」并在 3 秒内显示「撤销」按钮（`CanUndoClear` 属性驱动显隐）；`MainViewModel.StartUndoWindow` 用 3 秒延迟自动关闭撤销窗口，`UndoClearFiles` 取消窗口并恢复快照，配合 Ctrl+Z（F5）双入口撤销
- **批量多选与移除选中**（P5）：文件列表 `ListView` 开启 `SelectionMode="Extended"`（支持 Ctrl+A 全选、Shift 范围选择）；表头新增「移除选中」按钮，随 `HasSelection` 显隐；`MainViewModel.RemoveSelectedFiles` 批量移除并 Toast 提示数量
- **拖拽成功动画**（P6）：拖放真实路径后 `Window_Drop` 触发 `PlayDropSuccessAnimation`，拖拽区边框从拖拽高亮色（PrimaryBrush）经 700ms 渐变为主题边框色（`FillBehavior.Stop` 自动回落），`IsMotionOff` 时跳过；DropZone 边框容器补 `x:Name`
- **进度条宽度自适应**（U4）：进度轨道移除 `MinWidth/MaxWidth=300` 硬编码与右侧 80 冗余留白，改为 `HorizontalAlignment="Stretch"` 填满左列剩余空间；进度填充条移除 `Width="220"` 改为 Stretch，`ScaleX` 进度动画不受影响
- **模式卡片提取 UserControl**（U7）：新增 `Controls/ModeCard.xaml(.cs)`，以 `ModeIndex/ModeName/Description/SubDescription/IconText` 等依赖属性替代三段约 120 行重复 XAML；`MainWindow` 以三个 `<controls:ModeCard>` 渲染，`ModeCard_Click` 统一切换模式（ModeIndex 即 AppMode 枚举值），`SelectedModeIndex` 属性变化通过 `SyncModeCardSelection` 同步选中态
- **处理中动态窗口标题**（U8）：`MainWindow` 监听 `IsProcessing`/`ProcessCurrent`/`ProcessTotal`，处理中标题更新为「Doc2MD Converter - 正在处理 (3/10)」，完成或取消后恢复默认标题
- **大文件夹扫描节流**（R3）：`FileScanService` 进度上报改为 100ms 节流（`Stopwatch` 控制），循环结束强制补报一次保证计数准确；新增 `ConversionSettings.MaxScanFileCount` 配置（默认 5000，`ConfigService` 规范化下限 1），达到上限停止扫描并在 `FolderScanResult.Truncated` 标记；扫描完成后 UI 提示「已达扫描上限，仅加载前 N 个文件」

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