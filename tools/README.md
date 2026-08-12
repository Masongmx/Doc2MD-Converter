---
AIGC:
  ContentProducer: '001191110102MAD55U9H0F10002'
  ContentPropagator: '001191110102MAD55U9H0F10002'
  Label: '1'
  ProduceID: '6fe97fce-73ff-44c0-b574-b04c69b89aa9'
  PropagateID: '6fe97fce-73ff-44c0-b574-b04c69b89aa9'
  ReservedCode1: 'b918819d-1825-48a0-a949-7cef31e3ec97'
  ReservedCode2: 'b918819d-1825-48a0-a949-7cef31e3ec97'
---

# 离线转换引擎部署（绿色便携版）

本目录存放随程序分发的第三方便携工具，全部离线运行、免安装。程序按以下目录结构自动探测：

```text
tools/
├── LibreOffice/                          # 旧式 Office (.doc/.xls/.ppt) 转换引擎
│   └── program/
│       ├── soffice.com                   # 程序实际调用的 headless 入口
│       └── soffice.exe                   # GUI 启动器（headless 场景不要直接调用）
│
├── OCRmyPDF/                             # OCR 引擎 - 完整版（约 990MB）
│   ├── Scripts/ocrmypdf.exe              # 程序查找的入口
│   ├── python.exe                        # 内置 Python 运行环境
│   └── tesseract/                        # 内置 Tesseract（含 chi_sim + eng 语言包）
│
└── OCRmyPDF-slim/                        # OCR 引擎 - 精简版（约 300MB）
    ├── Scripts/ocrmypdf.exe              # 程序入口（同为独立 venv）
    └── tesseract/                        # 内置 Tesseract（含 chi_sim + eng）
```

## 两种 OCR 形态选择

| 形态 | 体积 | 说明 |
|------|------|------|
| `OCRmyPDF`（完整版） | ~990MB | 自带完整 Python 运行环境，兼容性最好，适合复杂环境 |
| `OCRmyPDF-slim`（精简版） | ~300MB | 干净 venv，体积小，适合快速分发 |

只需放入其中任意一个即可，程序自动识别。两个都放时优先使用完整版。

## 说明

- LibreOffice 用于 `.doc/.xls/.ppt` 旧式二进制 Office 文件的离线转换。
- OCRmyPDF 用于扫描型 PDF 的 OCR 识别；内置 Tesseract 及 `chi_sim` 简体中文语言包，程序使用 `chi_sim+eng` 识别语言组合，全程离线。
- 程序启动时会自动为 OCRmyPDF 进程设置 `PATH` 与 `TESSDATA_PREFIX`，指向内置 Tesseract，无需手动配置。
- 未放置对应工具时，程序在用到该功能时给出明确的配置提示，其余功能不受影响。

> AI生成