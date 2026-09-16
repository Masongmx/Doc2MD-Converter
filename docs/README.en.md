<div align="center">

# Doc2MD Converter

**Offline Document Conversion & GB/T 9704-2012 Chinese Official Document Typography Engine for Windows**

[![Release](https://img.shields.io/badge/release-v1.1.0-blue.svg)](https://github.com/Masongmx/Doc2MD-Converter/releases)
[![License](https://img.shields.io/badge/license-MIT-green.svg)](../LICENSE)
[![.NET 8](https://img.shields.io/badge/.NET-8.0-512BD4)](https://dotnet.microsoft.com/)
[![Platform](https://img.shields.io/badge/platform-Windows%2010%20%7C%2011%20(x64)-0078D6)](https://github.com/Masongmx/Doc2MD-Converter)
[![Offline](https://img.shields.io/badge/privacy-100%25%20Local%20Offline-success)](../SECURITY.md)
[![Tests](https://img.shields.io/badge/tests-355%20passed-brightgreen)](../tests/)

[中文](../README.md) · [Changelog](../CHANGELOG.md) · [Contributing Guide](../CONTRIBUTING.md) · [Security Policy](../SECURITY.md)

</div>

---

## 🌟 Key Highlights

Doc2MD Converter is a high-performance local offline conversion and document typesetting tool tailored for enterprise, government, and RAG knowledge base workflows:

- 🔒 **100% Local & Offline**: Zero telemetry, zero cloud API dependencies, documents never leave your machine — fully air-gap network and confidential data ready.
- 🤖 **Dual-Track AI & Human Readability**:
  - **For AI/RAG**: Generates clean YAML Frontmatter, structured headings, and preserves tables/images for seamless LLM & vector search ingestion;
  - **For Humans**: Built-in **Humanizer Pipeline** automatically generates TOC with anchor links, fixes skipped heading levels, aligns tables, formats nested lists, and highlights official document metadata.
- 🏛️ **National Standard Document Formatting**: Strictly adheres to **GB/T 9704-2012** (Chinese Party & Government Official Document Standards), supporting custom Word template style injection.
- 🛡️ **Failure Root-Cause Diagnosis Subsystem**: Intelligently classifies **8 categories of errors** (process lock, encryption, missing dependencies, OOM, corrupted files, etc.) with actionable advice, Windows user path sanitization, and local persistence.
- 🧹 **Deep AIGC Watermark Scrubbing**: Automatically detects and strips 6 types of AI tracking watermarks, metadata blocks, and invisible zero-width characters.

---

## 🖥️ Screenshot

![Main Window](screenshots/main-window.png)

---

## 🚀 Quick Start

### Option 1: Direct Download (No build tools required)

Visit [GitHub Releases](https://github.com/Masongmx/Doc2MD-Converter/releases) to download:

| Package Type | Filename | Best For | Description |
| :--- | :--- | :--- | :--- |
| **Portable Green Zip (Recommended)** | [`Doc2MD-Converter-v1.1.0-win-x64.zip`](https://github.com/Masongmx/Doc2MD-Converter/releases/latest) | Plug & Play / USB drive | Self-contained single executable with .NET 8 runtime included |
| **Standalone Executable** | `Doc2MD.Converter.exe` | Quick desktop launch | Portable win-x64 executable, double-click to run |
| **Full Installer** | `Doc2MD-Converter-1.0.0-Setup.exe` | Full offline bundle | Includes LibreOffice portable & OCR engines |

### Option 2: Build from Source

```powershell
# 1. Clone the repository
git clone https://github.com/Masongmx/Doc2MD-Converter.git
cd Doc2MD-Converter

# 2. Build the solution
dotnet build Doc2MD.Converter.slnx -c Release

# 3. Run all unit & scenario tests (355 tests)
dotnet test Doc2MD.Converter.slnx -c Release --no-build

# 4. Run the desktop app
dotnet run --project src/Doc2MD.Converter.App
```

---

## 📊 Feature Comparison

| Dimension | Doc2MD Converter | Generic Pandoc | Python Scripts | Cloud Converters |
| :--- | :---: | :---: | :---: | :---: |
| **Privacy & Security** | **100% Local & Offline** | Local offline | Local offline | ❌ Server upload |
| **GB/T 9704-2012 Standards** | **Native built-in + Template injection** | ❌ Manual styling needed | ❌ None | ❌ Costly customization |
| **Humanizer Pipeline** | **TOC / Alignment / Highlighting** | ❌ Source dependent | ❌ Raw text only | ❌ |
| **Failure Diagnosis & Sanitization** | **8 categories + Path anonymization** | ❌ Raw error codes | ❌ Unhandled exceptions | ❌ Vague errors |
| **AIGC Watermark Stripping** | **6 deep scrubbing patterns** | ❌ None | ❌ None | ❌ None |
| **Multi-Thread Concurrency** | **Context-isolated thread-safety** | Process isolated | Script-dependent | Token/concurrency metered |
| **Deployment Ease** | **Single file, no dependencies** | Complex environment | Python/pip setup required | API key required |

---

## 🛠️ Three Work Modes

```mermaid
flowchart TD
    A[Input Files / Folders] --> ModeSelect{Select Work Mode}
    
    subgraph Mode1 [1. Documents to Markdown (ToMarkdown)]
        M1_In[Word / PDF / Excel / PPT / TXT] --> M1_Parse[Parsers & OCR]
        M1_Parse --> M1_Post[Post-processing: AIGC Scrubbing & Metadata]
        M1_Post --> M1_Human[Humanizer Pipeline: TOC / Headings / Tables]
        M1_Human --> M1_Out[Output GFM Markdown & Metadata Package]
    end
    
    subgraph Mode2 [2. Markdown to Official DOCX (MarkdownToDocx)]
        M2_In[Markdown File] --> M2_Pipeline[Semantic Parsing & Template Pipeline]
        M2_Pipeline --> M2_Check[Format Compliance Checker]
        M2_Check --> M2_Out[Output GB/T 9704 Compliant DOCX]
    end
    
    subgraph Mode3 [3. DOCX One-Click Typesetting (FormatDoc)]
        M3_In[Unformatted Word Document] --> M3_Inject[Custom Template Style Merge]
        M3_Inject --> M3_Format[Fonts/Sizes/Spacing/Margin Normalization]
        M3_Format --> M3_Out[Output Typeset DOCX]
    end
    
    ModeSelect -->|Mode 1| Mode1
    ModeSelect -->|Mode 2| Mode2
    ModeSelect -->|Mode 3| Mode3
    
    M1_Parse -.->|Exception| FailureSubsystem[Failure Diagnosis & Retry Subsystem]
```

---

## 🏗️ Architecture & Project Layout

```
Doc2MD.Converter.slnx
├── src/
│   ├── Doc2MD.Converter.Core/      # Pure .NET 8 BCL Core Engine (No UI dependencies)
│   │   ├── Failures/               # Root cause diagnoser, path sanitizer, retry orchestrator
│   │   ├── Humanizer/              # Readability pipeline (TOC/Headings/Tables/Lists/Highlights)
│   │   ├── Parsers/                # Parsers (PDF/Word/Excel/PPT/Text)
│   │   ├── Pipeline/               # Markdown -> DOCX semantic rendering & checker
│   │   ├── Services/               # Conversion service, metadata extractor, AIGC filter
│   │   └── Models/                 # Shared domain models
│   └── Doc2MD.Converter.App/       # WPF Modern Desktop Client (.NET 8 Windows x64)
│       ├── ViewModels/             # MVVM ViewModels
│       ├── Controls/               # Custom controls (ModeCard, etc.)
│       └── Resources/              # Localization dictionaries
└── tests/
    ├── Doc2MD.Converter.Core.Tests/# Core engine tests (325 tests)
    └── Doc2MD.Converter.App.Tests/ # UI ViewModel & scanner tests (30 tests)
```

---

## ⌨️ Keyboard Shortcuts

| Shortcut | Action | Description |
| :--- | :--- | :--- |
| <kbd>Ctrl</kbd> + <kbd>O</kbd> | Add Files | Open file picker dialog |
| <kbd>Ctrl</kbd> + <kbd>Shift</kbd> + <kbd>O</kbd> | Add Folder | Import an entire folder recursively |
| <kbd>Ctrl</kbd> + <kbd>Enter</kbd> | Start | Trigger batch conversion or formatting |
| <kbd>Esc</kbd> | Cancel | Gracefully abort active processing |
| <kbd>Ctrl</kbd> + <kbd>Z</kbd> | Undo Clear | Restore cleared file list within 3-second window |
| <kbd>F5</kbd> | Refresh | Re-scan working directory for changes |

---

## 🤝 Contributing

We welcome community contributions! Please read our [Contributing Guide](../CONTRIBUTING.md) for details on code style, testing, and the pull request process.

---

## 📄 License

This project is open-sourced under the [MIT License](../LICENSE).
