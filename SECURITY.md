# 安全政策 (Security Policy)

## 🔒 隐私与离线承诺

**Doc2MD Converter** 是一款 **100% 纯本地运行的离线办公工具**：
1. **零数据外发**：应用不会向任何远程服务器或云端上传您的文档内容、元数据或个人信息。
2. **本地文件隔离**：内置 `SecurityPolicyService` 防止路径穿越攻击与恶意保留文件名写入。
3. **网络活动仅限检查更新**：仅在用户主动触发或设置允许时，通过 GitHub Releases API 检查新版本 Tag。

---

## 🛡️ 漏洞报告

如果您在本项目中发现了任何安全漏洞或隐私风险，请通过以下方式联系我们：

- **GitHub Security Advisory**：在 GitHub 仓库页面提交 Private Vulnerability Report（私密漏洞报告）
- **Issue 提报**：对于非敏感的常规安全改进建议，可直接提交 Issue。

我们将在收到报告后 48 小时内完成评估并给出答复。
