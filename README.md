---
AIGC:
  ContentProducer: '001191110102MAD55U9H0F10002'
  ContentPropagator: '001191110102MAD55U9H0F10002'
  Label: '1'
  ProduceID: 'd5e3758c-f3fa-4657-b9af-33a2fdcb8e2f'
  PropagateID: 'd5e3758c-f3fa-4657-b9af-33a2fdcb8e2f'
  ReservedCode1: '8d7f4a9d-a035-44e3-8dca-d30ae208b4ce'
  ReservedCode2: '8d7f4a9d-a035-44e3-8dca-d30ae208b4ce'
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
```

## 离线能力

- 默认不联网，文档不上传
- 支持自包含 win-x64 发布
- LibreOffice、OCR 等外部工具从本地路径调用
- 缺失依赖时提供清晰提示

## 许可证

见 LICENSE 文件。

> AI生成