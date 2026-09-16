# 贡献指南 (Contributing Guide)

感谢你对 **Doc2MD Converter** 项目的关注与贡献！开源社区的繁荣离不开每一位开发者的参与。

---

## 🛠️ 开发环境要求

- **操作系统**：Windows 10 / 11 (x64)
- **开发工具**：Visual Studio 2022 (17.8+) 或 Visual Studio Code / Rider
- **SDK**：.NET 8.0 SDK (8.0.x)
- **工作负载**：.NET 桌面开发 (WPF)

---

## 🚀 本地构建与测试

1. **克隆仓库**：
   ```bash
   git clone https://github.com/Masongmx/Doc2MD-Converter.git
   cd Doc2MD-Converter
   ```

2. **恢复依赖并构建**：
   ```bash
   dotnet restore Doc2MD.Converter.slnx
   dotnet build Doc2MD.Converter.slnx --configuration Debug
   ```

3. **运行全部单元测试**：
   ```bash
   dotnet test tests/Doc2MD.Converter.Core.Tests/
   ```

4. **运行 GUI 应用**：
   ```bash
   dotnet run --project src/Doc2MD.Converter.App/
   ```

---

## 📜 代码规范与架构准则

- **分层架构**：
  - `Doc2MD.Converter.Core`：跨平台核心业务逻辑（文档解析、排版流水线、安全策略等），严禁引用任何 `System.Windows.*` 或 WPF 依赖。
  - `Doc2MD.Converter.App`：WPF 桌面呈现层，遵循 MVVM 模式，通过 DI 容器接入核心服务。
- **线程安全**：
  - `IDocumentParser` 实现类在 DI 容器中为单例模式，解析方法严禁依赖实例级可变状态，所有解析上下文需通过方法内变量传递。
- **测试覆盖**：
  - 任何针对解析规则、安全策略、格式化管线的修复或新增功能，均须在 `Doc2MD.Converter.Core.Tests` 编写对应的 xUnit 单元测试。

---

## 🔀 提交流程

1. Fork 本仓库并新建功能分支：`git checkout -b feature/your-feature-name`
2. 编写代码并确保所有自动化测试通过 (`dotnet test`)
3. 提交变更并推送至你的 Fork 仓库
4. 提交 Pull Request 并详细填写 PR 模板
