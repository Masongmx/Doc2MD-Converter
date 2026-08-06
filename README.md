---
AIGC:
  ContentProducer: '001191110102MAD55U9H0F10002'
  ContentPropagator: '001191110102MAD55U9H0F10002'
  Label: '1'
  ProduceID: '4cd69d34-fd2c-4b06-af20-e7458a87a997'
  PropagateID: '4cd69d34-fd2c-4b06-af20-e7458a87a997'
  ReservedCode1: 'a1b80fcc-352c-4b17-b790-3e1301af16d7'
  ReservedCode2: 'a1b80fcc-352c-4b17-b790-3e1301af16d7'
---

# Doc2MD Converter

可完全离线运行的 Windows 文档转换与中文公文排版工具。

## 产品定位

批量文档输入 → 格式转换/公文排版 → 输出到指定目录

## 核心功能

1. **文档转 Markdown** — DOC/DOCX、PDF、XLS/XLSX、PPT/PPTX、TXT/Markdown
2. **Markdown 转公文 DOCX** — 符合 GB/T 9704-2012 标准
3. **DOC/DOCX 一键规范排版** — 中文公文版式自动规范化

## 技术架构

```
Doc2MD.Converter.slnx
├── src/Doc2MD.Converter.Core/    # 核心引擎（net8.0，无UI依赖）
├── src/Doc2MD.Converter.App/     # WPF 桌面应用（net8.0-windows）
├── src/Doc2MD.Converter.Cli/     # 命令行接口（net8.0）
└── tests/                        # 单元测试
```

## 构建与运行

```powershell
# 构建
dotnet build Doc2MD.Converter.slnx

# 运行测试
dotnet test

# 运行 GUI
dotnet run --project src/Doc2MD.Converter.App

# 运行 CLI
dotnet run --project src/Doc2MD.Converter.Cli -- convert input.docx
```

## 离线能力

- 默认不联网，文档不上传
- 支持自包含 win-x64 发布
- LibreOffice、OCR 等外部工具从本地路径调用
- 缺失依赖时提供清晰提示

## 许可证

见 LICENSE 文件。

> AI生成