# 离线转换引擎部署

本目录不包含第三方二进制文件。发布安装包时按需放入：

```text
tools/
├── LibreOffice/
│   └── program/
│       └── soffice.exe
└── OCRmyPDF/
    └── ocrmypdf.exe
```

LibreOffice 用于 `.doc/.xls/.ppt` 旧式二进制 Office 文件的离线转换。

OCRmyPDF 用于扫描型 PDF；需要安装/打包 Tesseract 及简体中文语言数据 `chi_sim`。程序会使用 `chi_sim+eng` 识别语言组合，并在引擎缺失时向用户显示明确的配置提示。
