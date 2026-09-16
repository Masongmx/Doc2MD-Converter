<div align="center">

# Doc2MD Converter

**Windows 离线全能文档转换器与 GB/T 9704-2012 中文公文排版引擎**

[![Release](https://img.shields.io/badge/release-v1.1.0-blue.svg)](https://github.com/Masongmx/Doc2MD-Converter/releases)
[![License](https://img.shields.io/badge/license-MIT-green.svg)](LICENSE)
[![.NET 8](https://img.shields.io/badge/.NET-8.0-512BD4)](https://dotnet.microsoft.com/)
[![Platform](https://img.shields.io/badge/platform-Windows%2010%20%7C%2011%20(x64)-0078D6)](https://github.com/Masongmx/Doc2MD-Converter)
[![Offline](https://img.shields.io/badge/privacy-100%25%20Local%20Offline-success)](SECURITY.md)
[![Tests](https://img.shields.io/badge/tests-355%20passed-brightgreen)](tests/)

[English](docs/README.en.md) · [更新日志](CHANGELOG.md) · [贡献指南](CONTRIBUTING.md) · [安全政策](SECURITY.md) · [离线部署指南](docs/内网离线部署指南.md)

</div>

---

## 🌟 核心亮点

Doc2MD Converter 是一款面向政企办公、知识库构建（RAG）和日常文档处理的高性能本地离线转换与排版工具：

- 🔒 **100% 本地纯离线运行**：零遥测、零云端 API 依赖、文档内容绝不上传，符合涉密与内网安全要求。
- 🤖 **AI 与人类双轨可读设计 (Dual-Track)**：
  - **AI 侧**：输出规范的 YAML Frontmatter、结构化标题、保留完整表格与图片元数据，直通大模型与向量数据库；
  - **人类侧**：内置 **Humanizer 中间件管线**，智能生成 TOC 目录、修复跳级标题、对齐表格、规整多层嵌套列表、高亮公文关键要素。
- 🏛️ **国家公文标准排版**：严格遵循 **GB/T 9704-2012《党政机关公文格式》** 标准，支持自定义 Word 模板样式注入与双向同步。
- 🛡️ **生产级失败根因诊断机制**：针对文档占用锁、密码加密、引擎缺失、格式损坏等 **8 大类故障**精准识别并给出人类行动建议，配合 Windows 路径隐私脱敏与本地知识库持久化，彻底消除无效重复重试。
- 🧹 **深度 AIGC 水印清洗**：自动检测并剥离 Frontmatter 标记、行内 AIGC 标识、零宽隐形字符等 6 类大模型生成水印。

---

## 🖥️ 软件界面

![主界面截图](docs/screenshots/main-window.png)

---

## 🚀 快速开始

### 方式 1：直接下载使用（无需安装环境）

前往 [GitHub Releases](https://github.com/Masongmx/Doc2MD-Converter/releases) 下载最新发行版：

| 发行形态 | 文件名 | 适用场景 | 说明 |
| :--- | :--- | :--- | :--- |
| **便携绿色版（推荐）** | [`Doc2MD-Converter-v1.1.0-win-x64.zip`](https://github.com/Masongmx/Doc2MD-Converter/releases/latest) | 即开即用 / U 盘随身携带 | 解压即用，内置 .NET 8 运行时，单文件执行 |
| **独立可执行程序** | `Doc2MD.Converter.exe` | 桌面快速快捷启动 | 单文件 Self-Contained 架构，双击运行 |
| **完整安装版** | `Doc2MD-Converter-1.0.0-Setup.exe` | 深度依赖集成 | 内置 LibreOffice 便携版与 OCR 引擎组件 |

### 方式 2：从源码编译与开发

```powershell
# 1. 克隆仓库
git clone https://github.com/Masongmx/Doc2MD-Converter.git
cd Doc2MD-Converter

# 2. 编译解决方案
dotnet build Doc2MD.Converter.slnx -c Release

# 3. 运行全量自动化测试（355 个测试用例）
dotnet test Doc2MD.Converter.slnx -c Release --no-build

# 4. 运行桌面程序
dotnet run --project src/Doc2MD.Converter.App
```

---

## 📊 能力对比一览

| 特性维度 | Doc2MD Converter | 传统 Pandoc | 常见 Python 提取脚本 | 云端/商业转换器 |
| :--- | :---: | :---: | :---: | :---: |
| **运行隐私** | **100% 本地纯离线** | 本地离线 | 本地离线 | ❌ 上传服务器 |
| **公文标准排版 (GB/T 9704)** | **原生内置 + 模板注入** | ❌ 需手工写样式 | ❌ 无 | ❌ 需昂贵定制 |
| **双轨可读性增强 (Humanizer)** | **目录/对齐/高亮全自动** | ❌ 依赖源格式 | ❌ 纯文本提取 | ❌ |
| **失败根因自动诊断与脱敏** | **8 类精准识别 + 路径脱敏** | ❌ 仅输出错误码 | ❌ 异常中断 | ❌ 错误信息模糊 |
| **AIGC 生成水印清洗** | **6 类深度剥离** | ❌ 无 | ❌ 无 | ❌ 无 |
| **多线程并发安全性** | **方法级上下文隔离** | 进程隔离 | 取决于编写 | 依赖并发数计费 |
| **部署便利度** | **单文件免装运行环境** | 需安装复杂环境 | 需配 Python 依赖 | 需注册 API Key |

---

## 🛠️ 三大核心工作模式

```mermaid
flowchart TD
    A[输入文件/文件夹] --> ModeSelect{选择工作模式}
    
    subgraph Mode1 [1. 文档转 Markdown (ToMarkdown)]
        M1_In[Word / PDF / Excel / PPT / TXT] --> M1_Parse[解析引擎提取]
        M1_Parse --> M1_Post[后处理: AIGC 清洗 & 元数据提取]
        M1_Post --> M1_Human[Humanizer 管线: TOC / 标题规范 / 表格规整]
        M1_Human --> M1_Out[输出标准 GFM Markdown & 元数据包]
    end
    
    subgraph Mode2 [2. Markdown 转公文 (MarkdownToDocx)]
        M2_In[Markdown 文件] --> M2_Pipeline[语义解析 & 模板渲染管线]
        M2_Pipeline --> M2_Check[公文格式符合度检查]
        M2_Check --> M2_Out[输出符合 GB/T 9704 规范的 DOCX]
    end
    
    subgraph Mode3 [3. DOCX 一键规范排版 (FormatDoc)]
        M3_In[未排版 Word 文档] --> M3_Inject[自定义模板样式合并]
        M3_Inject --> M3_Format[字体/字号/行距/边距规范化]
        M3_Format --> M3_Out[输出已排版公文文档]
    end
    
    ModeSelect -->|模式 1| Mode1
    ModeSelect -->|模式 2| Mode2
    ModeSelect -->|模式 3| Mode3
    
    M1_Parse -.->|发生异常| FailureSubsystem[失败诊断与重试子系统: 8大根因识别 + 路径脱敏 + 策略编排]
```

### 1. 文档转 Markdown (`ToMarkdown`)
- 支持 **PDF**（文字型直解 / 扫描型 OCR 增强）、**Word**（`.docx` 原生 / 旧版 `.doc` 双重降级）、**Excel**（`.xlsx` / `.xls` 保留表头与数据网格）、**PPT**（`.pptx` / `.ppt`）与纯文本；
- 自动提取公文六大要素：**标题、发文字号、发文机关、成文日期、文档类型、主题关键词**；
- 质量评估系统：自动计算文档完整度、字数、表格数与图片资产，输出导入质量建议。

### 2. Markdown 转公文 DOCX (`MarkdownToDocx`)
- 严格按照《党政机关公文格式》自动排版为可直接打印下发的标准 `.docx`；
- 支持内置模板（正式公文 `official-report`、会议纪要 `meeting-minutes`）与自定义模板导入；
- 自动处理正文中文字体（小标宋 / 黑体 / 楷体 / 仿宋）、行距（固定 28.9pt）、字间距与版心边距。

### 3. DOC / DOCX 一键规范排版 (`FormatDoc`)
- 对现有杂乱的 Word 文档一键重构为国家公文标准版式；
- 支持自定义 `.docx` / `.dotx` 模板的样式注入合并（不破坏文档原有逻辑）；
- 自动规整页眉页脚、版记分隔线与奇偶页码。

---

## 🏗️ 架构与项目结构

```
Doc2MD.Converter.slnx
├── src/
│   ├── Doc2MD.Converter.Core/      # 纯 .NET 8 BCL 跨平台核心引擎（无 UI 依赖）
│   │   ├── Failures/               # 失败案例诊断、路径脱敏、仓储与重试策略编排
│   │   ├── Humanizer/              # 可读性优化管线 (TOC/跳级标题/表格/列表/公文高亮)
│   │   ├── Parsers/                # 各格式解析器 (PDF/Word/Excel/PPT/Text)
│   │   ├── Pipeline/               # Markdown -> DOCX 语义化渲染与合规检查
│   │   ├── Services/               # 转换主服务、公文元数据提取、AIGC 过滤、更新检查
│   │   └── Models/                 # 统一领域模型 (AppConfig / ConversionResult 等)
│   └── Doc2MD.Converter.App/       # WPF 现代化桌面客户端 (.NET 8 Windows x64)
│       ├── ViewModels/             # MVVM 视图模型 (MainViewModel / FileScanService)
│       ├── Controls/               # 模式卡片 (ModeCard) 等自定义组件
│       └── Resources/              # 多语言字典 (Strings.resx / Strings.en.resx)
└── tests/
    ├── Doc2MD.Converter.Core.Tests/# 核心引擎测试 (325 个用例：解析、排版、失败诊断、Humanizer)
    └── Doc2MD.Converter.App.Tests/ # 界面视图模型与文件扫描测试 (30 个用例)
```

---

## ⌨️ 快捷键指南

| 快捷键 | 功能 | 说明 |
| :--- | :--- | :--- |
| <kbd>Ctrl</kbd> + <kbd>O</kbd> | 添加文件 | 打开文件选择对话框 |
| <kbd>Ctrl</kbd> + <kbd>Shift</kbd> + <kbd>O</kbd> | 添加文件夹 | 批量导入整个目录 |
| <kbd>Ctrl</kbd> + <kbd>Enter</kbd> | 开始处理 | 立即触发转换/排版任务 |
| <kbd>Esc</kbd> | 取消任务 | 优雅中止当前正在进行的批量处理 |
| <kbd>Ctrl</kbd> + <kbd>Z</kbd> | 撤销清空 | 在 3 秒撤销窗口内快速恢复被清空的文件列表 |
| <kbd>F5</kbd> | 刷新当前目录 | 重新扫描当前工作目录文件变更 |

---

## ⚖️ 字体与合规说明

> [!NOTE]
> GB/T 9704-2012 国家公文标准推荐使用**方正小标宋简体**、**仿宋_GB2312**、**楷体_GB2312** 与 **黑体**。若在未预装此类商业字库的系统上运行，Word 会自动回落至系统内置字体。如用于商业环境，建议遵循字体厂商授权政策，或在排版方案中指定开源的**思源宋体 (Source Han Serif)** 与 **思源黑体 (Source Han Sans)**。

---

## 🤝 参与贡献

我们非常欢迎社区贡献！在提交代码前，请参阅 [CONTRIBUTING.md](CONTRIBUTING.md) 了解详细流程与规范。

1. **Fork** 本仓库；
2. 新建功能分支 (`git checkout -b feat/my-cool-feature`)；
3. 编写代码并补充自动化测试（确保 `dotnet test` 100% 绿灯）；
4. 提交更改 (`git commit -m 'feat: add some amazing feature'`)；
5. 推送至分支 (`git push origin feat/my-cool-feature`)；
6. 发起 **Pull Request**。

---

## 📄 开源许可证

本项目基于 [MIT License](LICENSE) 开源发布。
