# Doc2MD-Converter 真实场景测试体系设计文档（v1.1.0）

> **文档版本**：v1.0
> **撰写日期**：2026-09-14
> **撰写人**：严过关（Yan）/ QA 工程师
> **基线版本**：v1.0.0（2026-08-12 发布）
> **输入物**：
> - 增量 PRD：`doc2md-increment-prd-2026-09-14.md`（REQ-T-01~11 + TC-01~12）
> - 增量架构：`doc2md-increment-arch-2026-09-14.md`（§A 失败处理 + §B 可读性 + §C 测试架构）
> - 现有测试基线：`tests/Doc2MD.Converter.Core.Tests/`（15 文件、xUnit、FluentAssertions 不在）
> - 系统性审查报告：`系统性审查报告-2026-09-14.md`
> **目标版本**：v1.1.0（预计 2026-Q4 发布）
> **写作范围**：仅增量设计（真实场景测试体系），不动现有单测

---

## 0. 文档说明

### 0.1 与既有测试的关系

| 维度 | 既有单测 | 增量场景测试 | 边界 |
|------|----------|--------------|------|
| **目标** | 验证代码逻辑 | 验证用户能完成任务 | 不互相替代 |
| **粒度** | 方法/类 | 端到端任务链 | — |
| **数据** | Mock / 内存对象 | 真实夹具 + 文件 | 场景测试**不复用 Mock** |
| **运行** | `dotnet test`（默认） | `dotnet test --filter Category=Scenario` | 场景测试 CI 上标记 `[Trait("Flaky")]` |
| **失败定位** | 单元级 | 用户级（附 trace） | 场景失败 → 排查单测 |
| **覆盖率** | 行覆盖（目前 295+ 用例） | 任务链覆盖（≥30 场景） | 场景不替代行覆盖 |

### 0.2 与既有文档的关系

| 文档 | 关系 |
|------|------|
| PRD §4.3（REQ-T-01~11） | 本文档的**需求来源**；所有场景可追溯到 REQ-T-* / TC-* 编号 |
| 架构 §A.4（失败处理 Sequence） | 提供失败处理的**接口契约**（`FailureOrchestrator.HandleAsync`） |
| 架构 §B.3（Humanizer 双轨管线） | 提供可读性优化的**Pipeline 设计**（`HumanizerMiddleware.ApplyAsync`） |
| 现有测试项目 `Doc2MD.Converter.Core.Tests/` | 风格基线（xUnit + `[Fact]`/`[Theory]` + `Assert.Equal`） |

### 0.3 不在本设计范围

- ❌ 生产代码实现（由工程师接手 T01~T05）
- ❌ 单元测试扩展 `FailureHandling/*` 和 `Humanizer/*`（由 T05 的工程师产出）
- ❌ 真实 PDF/Word/Excel/PPT 二进制夹具的生成（需法务/数据团队授权）
- ❌ CI 流水线本身（GitHub Actions workflow 仅产出骨架）
- ❌ 录屏功能（PRD OQ-10 已决定 v1.1 不做）

---

## 1. 测试策略（Test Strategy）

### 1.1 单元测试 vs 场景测试边界

| 维度 | 单元测试（既有） | 场景测试（新增） |
|------|------------------|------------------|
| **测什么** | 类/方法的契约（输入→输出） | 用户任务的完成度（步骤→指标） |
| **举例** | `RuleBasedFailureDiagnoser.Diagnose(PdfDocumentEncryptedException)` 返回 `SourceFileEncrypted` | 用户将 5 份加密 PDF 加入列表 → 看到 5 个橙色"加密"徽章 + 跳过按钮 |
| **断言** | 枚举值 / 异常类型 / 字符串 | 毫秒 / 召回率 / 帧率 / 像素不可断言 |
| **失败可见性** | 单元报错（栈追踪） | 场景 trace（步骤录像 / 指标 JSON） |
| **运行频率** | 每次 PR | 每晚 + 发布前 |
| **CI 阻塞规则** | 任意失败 = 阻塞 | `[Trait("Flaky")]` 不阻塞；其他失败 = 阻塞 |

### 1.2 可观察体验指标量化方法

PRD §6 给出 6 类指标，本设计将每类映射到**测量方式 + 采集器 + 断言器**：

| PRD 指标 | 量化方式 | 测量器 | 断言器 |
|----------|----------|--------|--------|
| **首问解决率 ≥ 80%** | 失败案例的 `ResolvedBy` 在第一次尝试中非空的比例 | `FailureLibraryViewModel` 暴露 `ResolutionRate` 属性 | `MetricAssert.GreaterThan(rate, 0.80)` |
| **复制保留率 ≥ 90%** | 复制粘贴后段落/表格/列表结构保留率 | `CopyRetentionMetrics` 解析粘贴目标 DOM，与源 MD 对比 | `MetricAssert.GreaterThan(retention, 0.90)` |
| **AI 零回归** | 50 条黄金用例的 token diff 集合 | `AiRegressionMetrics` 计算 (added, removed, modified) tokens | `MetricAssert.ZeroRegression(tokenDiff)` |
| **30 场景通过率 ≥ 90%** | `PassedScenarios / TotalScenarios` | `ScenarioRunner` 汇总 | `MetricAssert.GreaterThan(passRate, 0.90)` |
| **场景耗时 ≤ 30s** | 单场景端到端 wall-clock | `Stopwatch` 包裹 `ScenarioContext.ExecuteAsync` | `MetricAssert.LessThan(elapsed, 30s)` |
| **TOC 可用性 ≥ 95%** | 点击 TOC 链接 → 跳转成功的次数 / 总次数 | `HumanReadableDriver` 用 FlaUI 点击链接 + 验证滚动 | `MetricAssert.GreaterThan(tocSuccessRate, 0.95)` |

### 1.3 测试金字塔调整建议

架构师已在 PRD §10 + 架构 §C 中提议独立 `ScenarioTests` 项目，本设计进一步细化：

```
                    ┌─────────────────────────────┐
                    │   E2E 真实场景测试 (30+)    │   ← 新增 ScenarioTests
                    │   慢、脆弱、用户视角         │   ← 仅跑黄金 + 标记 Flaky
                    ├─────────────────────────────┤
                    │   集成测试 (5+ 文件 E2E)    │   ← 既有 E2EPipelineTests
                    │   中速、聚焦 Pipeline       │
                    ├─────────────────────────────┤
                    │   单元测试 (295+ 既有 + 100) │   ← 既有 15 文件 + 新增 12 文件
                    │   快、稳定、覆盖率主导       │
                    └─────────────────────────────┘
```

**核心原则**：
1. **场景测试 ≤ 总测试时间 30%**：每个场景 ≤ 30s，30 场景 ≤ 15min（PRD NF-P-04 要求 ≤ 5min，故仅跑核心 10 场景）
2. **场景失败先排查单测**：场景测试只标记"哪一步失败"，不深挖原因
3. **场景不替代单测**：不允许为追求"场景通过"而删单测

### 1.4 CI 集成策略

| CI 触发 | 跑什么 | 阻塞规则 |
|---------|--------|----------|
| PR 提交 | 单元测试 + 5 条 P0 黄金场景 | 任一失败 = 阻塞 PR 合入 |
| 主干合并 | 单元测试 + 全部 30 场景 | `[Trait("Flaky")]` 不阻塞；其他失败 = 告警 |
| 发版前 | 单元测试 + 全部 30 场景 + 性能基准 | 任意指标退化 > 5% = 阻塞发版 |
| 每日定时 | 全部 30 场景 + 50 条 AI 黄金回归 | 失败 = 邮件告警 |

---

## 2. 30+ 真实场景设计（YAML 场景定义）

### 2.1 场景矩阵设计

| 场景维度 | 子分类 | 文档类型 | 边界条件 | 数量 |
|----------|--------|----------|----------|------|
| **阅读（长文档滚动）** | 短文档 TOC / 长文档 TOC / 大纲导航 | PDF / Word / Excel / PPT / TXT | 加密 / 损坏 / 扫描 / 超长 (>100MB) | 6 |
| **查阅（关键字定位）** | 单关键字 / 多关键字 / 正则 / 跨章节 | 同上 | 同上 | 5 |
| **复制（段落粘贴）** | 标题 / 段落 / 表格 / 列表 / 嵌套列表 | 同上 | 同上 | 8 |
| **分享（IM/邮件发送 MD）** | 邮件附件 / IM 文本 / 网盘分享 | 同上 | 同上 | 4 |
| **失败处理** | 加密 / 损坏 / 扫描 / 资源不足 / 依赖缺失 | PDF / Word / Excel / PPT | — | 5 |
| **AI 零回归** | 公文 / 报告 / 合同 / 技术文档 / 论文 | MD（已转换） | — | 2 |
| **总计** | — | — | — | **30** |

### 2.2 场景 YAML 定义规范

每条场景独立 YAML 文件，遵循 **YAML 1.2** 规范，**2 空格缩进**：

```yaml
# 场景元数据
id: SC-001                     # 场景编号（SC-001 ~ SC-030）
name: 阅读-短文档TOC跳转       # 场景名（中文 + 维度标识）
user_goal: 用户能在 3 步内找到"第三章 实施计划"
priority: P0                   # P0 / P1 / P2
category: reading              # reading / searching / copying / sharing / failure / regression

# 前置条件（含 fixture 引用）
preconditions:
  fixtures:
    - path: fixtures/pdf/short_report_5pages.pdf
      sha256: <必填>            # 用于场景重放校验
  app_state:
    - app_running: true
    - mode: ConvertToMd
    - output_dir: %TEMP%/scenario_001

# 操作步骤（用户视角）
actions:
  - step: 1
    actor: user
    action: 拖入 fixtures/pdf/short_report_5pages.pdf
    expected: 文件项出现在列表中
  - step: 2
    actor: user
    action: 点击"开始转换"
    expected: 进度条推进，~3s 内完成
  - step: 3
    actor: user
    action: 切换"增强阅读模式"开关
    expected: 预览面板顶部出现"## 目录"含 5 个章节链接
  - step: 4
    actor: user
    action: 点击目录中"## 第三章 实施计划"
    expected: 预览面板滚动到该章节，标题高亮

# 成功标准（可量化）
success_criteria:
  metric: TocJumpSuccess
  target: ">= 0.95"             # 95% 成功率
  timing:
    total_wall_clock: "<= 10s" # 端到端总耗时
    step_4_jump_latency: "<= 500ms"
  output_artifacts:
    - temp/scenario_001/output.md
    - temp/scenario_001/output.ai.md

# 证据记录
evidence:
  screenshots:
    - step_3_preview_with_toc.png
    - step_4_after_jump.png
  logs:
    - [Scenario] SC-001 step=3 elapsed=1200ms status=pass
    - [Scenario] SC-001 step=4 elapsed=480ms status=pass
  metrics_json: temp/scenario_001/metrics.json

# 失败处理
on_failure:
  capture_trace: true           # 自动捕获 FlaUI trace
  attach_artifacts:
    - last_screenshot.png
    - conversion_log.txt
  raise: ScenarioAssertionException(SC-001, step=4, expected=..., actual=...)
```

### 2.3 30 条场景清单（YAML 索引）

> 完整 YAML 定义见 `tests/Doc2MD.Converter.ScenarioTests/Scenarios/` 目录下各子目录，本节仅给**索引表 + 摘要**。

#### 2.3.1 阅读维度（SC-001 ~ SC-006，6 条）

| ID | 场景名 | 文档类型 | 边界条件 | 优先级 | 主要指标 |
|----|--------|----------|----------|--------|----------|
| SC-001 | 阅读-短文档TOC跳转 | PDF（5 页） | 常规 | P0 | TOC 跳转成功率 ≥ 95%、跳转延迟 ≤ 500ms |
| SC-002 | 阅读-长文档TOC可用性 | PDF（50 页） | 常规 | P0 | TOC 可用性 ≥ 95%、目录字数显示 |
| SC-003 | 阅读-Word大纲导航 | Word（10 页） | 常规 | P1 | 大纲层级正确率 ≥ 90% |
| SC-004 | 阅读-Excel多Sheet | Excel（5 Sheet） | 常规 | P1 | Sheet 间跳转成功率 ≥ 90% |
| SC-005 | 阅读-PPT幻灯片顺序 | PPT（20 张） | 常规 | P1 | 幻灯片顺序保留 100% |
| SC-006 | 阅读-超大PDF滚动 | PDF（100MB/500页） | 超长 | P0 | UI 不卡死（≥ 30 FPS）、进度条平滑 |

#### 2.3.2 查阅维度（SC-007 ~ SC-011，5 条）

| ID | 场景名 | 文档类型 | 边界条件 | 优先级 | 主要指标 |
|----|--------|----------|----------|--------|----------|
| SC-007 | 查阅-单关键字定位 | PDF（公文） | 常规 | P0 | 关键字定位 ≤ 2s、召回率 ≥ 95% |
| SC-008 | 查阅-多关键字高亮 | Word（报告） | 常规 | P0 | 所有出现位置高亮、召回率 ≥ 95% |
| SC-009 | 查阅-日期正则搜索 | PDF（合同） | 常规 | P1 | 日期格式识别 ≥ 90% |
| SC-010 | 查阅-跨章节搜索 | PDF（论文） | 常规 | P1 | 跨章节结果完整 |
| SC-011 | 查阅-扫描PDF无文字 | PDF（扫描） | 扫描 | P0 | 明确提示"需 OCR"、不假装搜索 |

#### 2.3.3 复制维度（SC-012 ~ SC-019，8 条）

| ID | 场景名 | 文档类型 | 边界条件 | 优先级 | 主要指标 |
|----|--------|----------|----------|--------|----------|
| SC-012 | 复制-段落粘贴保留 | PDF（公文） | 常规 | P0 | 段落结构保留 ≥ 95% |
| SC-013 | 复制-表格行列一致 | Word（5×3 表） | 常规 | P0 | 表格行列数一致 ≥ 95% |
| SC-014 | 复制-嵌套列表层级 | TXT（3 级嵌套） | 常规 | P0 | 嵌套层级正确 ≥ 95% |
| SC-015 | 复制-标题样式保留 | PDF（H2 标题） | 常规 | P0 | 标题样式保留 ≥ 90% |
| SC-016 | 复制-代码块到VSCode | TXT（代码） | 常规 | P1 | 代码块完整、语法高亮 |
| SC-017 | 复制-图片占位保留 | PDF（含 3 图） | 常规 | P1 | 图片引用占位完整 |
| SC-018 | 复制-加密文件提示 | PDF（加密） | 加密 | P0 | 错误信息清晰（非堆栈） |
| SC-019 | 复制-损坏文件兜底 | PDF（损坏） | 损坏 | P0 | 自动 OCR 兜底、成功率 ≥ 70% |

#### 2.3.4 分享维度（SC-020 ~ SC-023，4 条）

| ID | 场景名 | 文档类型 | 边界条件 | 优先级 | 主要指标 |
|----|--------|----------|----------|--------|----------|
| SC-020 | 分享-邮件附件 MD | PDF（10 页） | 常规 | P1 | 附件可正常打开、TOC 渲染 |
| SC-021 | 分享-IM 文本渲染 | Word（5 页） | 常规 | P1 | Typora 渲染一致性 ≥ 90% |
| SC-022 | 分享-压缩包输出 | 混合 10 文件 | 常规 | P1 | 压缩包完整性、目录结构保留 |
| SC-023 | 分享-空文件跳过 | TXT（0 字节） | 空文件 | P1 | 友好提示，不崩溃 |

#### 2.3.5 失败处理维度（SC-024 ~ SC-028，5 条）

| ID | 场景名 | 文档类型 | 边界条件 | 优先级 | 主要指标 |
|----|--------|----------|----------|--------|----------|
| SC-024 | 失败-加密 PDF 分类 | PDF（密码保护） | 加密 | P0 | UI 显示加密徽章 + 跳过按钮、首问解决率 ≥ 80% |
| SC-025 | 失败-扫描型 PDF OCR | PDF（扫描） | 扫描 | P0 | 自动 OCR、成功 ≥ 70% |
| SC-026 | 失败-LibreOffice 未安装 | Word（.doc） | 依赖缺失 | P0 | UI 弹安装按钮、5s 内打开浏览器 |
| SC-027 | 失败-资源不足降级 | 批量 200 PDF | 资源不足 | P0 | 自动单线程、不 OOM |
| SC-028 | 失败-文件被Word锁定 | Word（已打开） | 文件占用 | P0 | 明确提示"关闭 Word"、30s 后可重试 |

#### 2.3.6 AI 零回归维度（SC-029 ~ SC-030，2 条）

| ID | 场景名 | 文档类型 | 边界条件 | 优先级 | 主要指标 |
|----|--------|----------|----------|--------|----------|
| SC-029 | 回归-AI 路径零退化 | 50 条黄金 MD | 常规 | P0 | token diff ≤ 容忍阈值、零回归 |
| SC-030 | 回归-人类路径增强 | 50 条黄金 MD | 常规 | P0 | 增强项生效、TOC/高亮/占位正确 |

### 2.4 完整 YAML 文件清单

> **生成位置**：`tests/Doc2MD.Converter.ScenarioTests/Scenarios/` 目录下分类子目录。

```
tests/Doc2MD.Converter.ScenarioTests/Scenarios/
├── reading/
│   ├── sc-001-short-pdf-toc.yaml
│   ├── sc-002-long-pdf-toc.yaml
│   ├── sc-003-word-outline.yaml
│   ├── sc-004-excel-multisheet.yaml
│   ├── sc-005-ppt-slides.yaml
│   └── sc-006-huge-pdf-scroll.yaml
├── searching/
│   ├── sc-007-single-keyword.yaml
│   ├── sc-008-multi-keyword.yaml
│   ├── sc-009-date-regex.yaml
│   ├── sc-010-cross-section.yaml
│   └── sc-011-scanned-pdf-nokeyword.yaml
├── copying/
│   ├── sc-012-paragraph-paste.yaml
│   ├── sc-013-table-paste.yaml
│   ├── sc-014-nested-list.yaml
│   ├── sc-015-heading-style.yaml
│   ├── sc-016-code-block.yaml
│   ├── sc-017-image-placeholder.yaml
│   ├── sc-018-encrypted-paste.yaml
│   └── sc-019-corrupted-ocr.yaml
├── sharing/
│   ├── sc-020-email-attachment.yaml
│   ├── sc-021-im-typora-render.yaml
│   ├── sc-022-zip-output.yaml
│   └── sc-023-empty-file.yaml
├── failure/
│   ├── sc-024-encrypted-pdf-classify.yaml
│   ├── sc-025-scanned-pdf-ocr.yaml
│   ├── sc-026-libreoffice-missing.yaml
│   ├── sc-027-resource-exhausted.yaml
│   └── sc-028-file-locked.yaml
└── regression/
    ├── sc-029-ai-zero-regression.yaml
    └── sc-030-human-path-enhancement.yaml
```

### 2.5 夹具规范（Fixtures）

#### 2.5.1 夹具目录结构

```
tests/Doc2MD.Converter.ScenarioTests/Fixtures/
├── pdf/
│   ├── short_report_5pages.pdf          # SC-001 / SC-007 / SC-012
│   ├── long_report_50pages.pdf          # SC-002
│   ├── gov_document_request.pdf         # SC-007 / SC-012 / SC-015
│   ├── contract_with_dates.pdf          # SC-009 / SC-013
│   ├── paper_thesis_30pages.pdf         # SC-010
│   ├── scanned_no_text.pdf              # SC-011 / SC-019 / SC-025
│   ├── with_images_3pics.pdf            # SC-017
│   ├── encrypted_password.pdf           # SC-018 / SC-024
│   ├── corrupted_header.pdf             # SC-019
│   └── huge_100mb_500pages.pdf          # SC-006 (按需下载)
├── word/
│   ├── outline_10pages.docx             # SC-003
│   └── with_table_5x3.docx              # SC-013
├── excel/
│   └── multi_sheet_5.xlsx               # SC-004
├── ppt/
│   └── slides_20.pptx                   # SC-005
├── txt/
│   ├── plain_readme.txt                 # 通用
│   ├── with_code_block.txt              # SC-016
│   ├── nested_list_3levels.txt          # SC-014
│   └── empty_0bytes.txt                 # SC-023
├── edge/
│   ├── pdf_with_password.docx           # SC-026 (.doc 需 LibreOffice)
│   ├── ppt_already_open.pptx            # SC-028
│   └── zip_with_10files.zip             # SC-022
└── README.md                            # 脱敏规范
```

#### 2.5.2 脱敏规范（Fixtures/README.md）

```markdown
# 真实场景测试夹具脱敏规范

## 原则
- **零真实信息**：所有人名、身份证号、电话、邮箱、地址、银行卡号必须虚构或脱敏
- **可识别模式**：使用 `测试用户A`、`138****0001` 等占位符
- **小体积**：单文件 ≤ 10 MB（除非特殊场景如 SC-006）

## 命名规范
- 业务场景：`{type}_{scenario_hint}.{ext}`
- 边界场景：`{type}_{boundary}.{ext}`（如 `encrypted_password.pdf`）

## 大文件处理
- 100MB PDF 等大文件放 LFS 或按需下载
- CI 用 `tests/fixtures/.gitattributes` 标记 LFS

## 来源
- v1.0.0 已有：`tests/fixtures/sample_meeting.md` / `sample_report.md`
- 新增需走 `prepare-fixtures.ps1` 脚本生成
```

---

## 3. AI 零回归黄金用例集（基于架构 §B.3）

### 3.1 设计目标

**核心约束**（PRD US-M5）：可读性优化**不能破坏** AI 解析路径。架构 §B.3 引入双轨管线（AI 路径 / 人类路径），本设计提供**50 条黄金用例**确保：

1. AI 路径输出的 MD **与原 MD 完全一致**（byte-level 或容忍差异集合内）
2. 人类路径输出的 MD **在前者基础上增强**（TOC / 高亮 / 占位）

### 3.2 黄金用例目录

```
tests/Doc2MD.Converter.ScenarioTests/GoldenCorpus/
├── ai-golden-001.md    # 短公文（请示）
├── ai-golden-002.md    # 长公文（报告）
├── ai-golden-003.md    # 会议纪要
├── ai-golden-004.md    # 通知
├── ai-golden-005.md    # 通报
├── ai-golden-006.md    # 函
├── ai-golden-007.md    # 决议
├── ai-golden-008.md    # 公告
├── ai-golden-009.md    # 合同（中文）
├── ai-golden-010.md    # 合同（中英双语）
├── ai-golden-011.md    # 技术文档（API 参考）
├── ai-golden-012.md    # 技术文档（教程）
├── ai-golden-013.md    # 学术论文（中文）
├── ai-golden-014.md    # 学术论文（英文）
├── ai-golden-015.md    # 产品需求文档
├── ai-golden-016.md    # 项目周报
├── ai-golden-017.md    # 财务报表（文字）
├── ai-golden-018.md    # 财务报表（含表格）
├── ai-golden-019.md    # 用户手册
├── ai-golden-020.md    # 操作指南
├── ai-golden-021.md    # 邮件正文（多回复）
├── ai-golden-022.md    # 聊天记录（导出）
├── ai-golden-023.md    # 法律条文
├── ai-golden-024.md    # 标准规范
├── ai-golden-025.md    # 培训教材
├── ai-golden-026.md    # 新闻稿
├── ai-golden-027.md    # 博客文章
├── ai-golden-028.md    # 演讲稿
├── ai-golden-029.md    # 工作总结
├── ai-golden-030.md    # 个人简历
├── ai-golden-031.md    # 包含表格（3x3）
├── ai-golden-032.md    # 包含表格（10x5 宽表）
├── ai-golden-033.md    # 包含嵌套列表（3 级）
├── ai-golden-034.md    # 包含代码块（Python）
├── ai-golden-035.md    # 包含代码块（JSON）
├── ai-golden-036.md    # 包含引用块（多层）
├── ai-golden-037.md    # 包含图片占位
├── ai-golden-038.md    # 包含链接
├── ai-golden-039.md    # 包含数学公式占位
├── ai-golden-040.md    # 包含 Mermaid 占位
├── ai-golden-041.md    # 中文公文（含文号）
├── ai-golden-042.md    # 中文公文（含机关名）
├── ai-golden-043.md    # 中文公文（含日期）
├── ai-golden-044.md    # 中文公文（含金额）
├── ai-golden-045.md    # 英文文档（含 AIGC 水印）
├── ai-golden-046.md    # 含零宽字符的 AI 输出
├── ai-golden-047.md    # 含 UUID 行的 AI 输出
├── ai-golden-048.md    # 含 YAML frontmatter
├── ai-golden-049.md    # 含 block_id 注释
└── ai-golden-050.md    # 含混合水印（多重 AI 特征）
```

### 3.3 黄金用例基线采集流程

```
┌──────────────────────────────────────────────────────────────────┐
│  Step 1: 在 v1.0.0 上跑 50 篇样本文档                            │
│          → ConversionQuality 评分 ≥ 0.9 的文档入选黄金集          │
│                                                                   │
│  Step 2: 记录每篇文档的 base MD（AI 路径输出）                    │
│          → 保存为 ai-golden-NNN.expected.md（黄金基线）            │
│                                                                   │
│  Step 3: 记录每篇文档的 enhanced MD（人类路径输出）                │
│          → 保存为 ai-golden-NNN.enhanced.expected.md              │
│                                                                   │
│  Step 4: 记录每篇文档的 token diff（基线 vs 后续版本）            │
│          → 保存为 ai-golden-NNN.baseline.json                     │
└──────────────────────────────────────────────────────────────────┘
```

### 3.4 容忍差异集合（Allowed Differences）

跑 `Humanizer` 前后 diff 校验时，**允许以下差异**（不计入回归）：

| 差异类型 | 示例 | 是否计入回归 |
|----------|------|--------------|
| 新增空行 | 段落间多 1 个空行 | ❌ 不计入（架构 REQ-M-02） |
| 标题前缀 | `第一章` → `# 第一章` | ❌ 不计入（架构 REQ-M-01） |
| 图片占位 | `<图片>` → `![图片-1](images/pic.png)` | ❌ 不计入（架构 REQ-M-08） |
| TOC 注入 | 文首新增 `## 目录` 块 | ❌ 不计入（架构 REQ-M-06） |
| 关键信息加粗 | `〔2026〕12号` → `**〔2026〕12号**` | ❌ 不计入（架构 REQ-M-07） |
| 首行缩进 | 中文段首加 `　　` | ❌ 不计入（架构 REQ-M-10） |
| **移除原有空行** | 列表项间原有空行被移除 | ✅ **计入回归** |
| **修改 token 顺序** | 段落内 token 重排 | ✅ **计入回归** |
| **修改代码块语言标识** | ` ``` ` → ` ```csharp ` | ✅ **计入回归**（除非源 MD 标记缺失） |
| **修改表格分隔符** | `\| --- \|` → `\| :--- \|` | ❌ 不计入（架构 REQ-M-03） |
| **修改列表编号** | `1.` → `1)` | ✅ **计入回归**（语义变更） |

### 3.5 零回归判定算法

```csharp
// AiRegressionMetrics.cs 伪代码（骨架）
public static RegressionReport Compare(string expected, string actual)
{
    var expectedTokens = Tokenize(expected);
    var actualTokens = Tokenize(actual);
    var rawDiff = ComputeDiff(expectedTokens, actualTokens);

    // 应用容忍规则
    var toleratedDiff = rawDiff
        .Where(d => !IsAllowedDifference(d))   // 过滤掉 §3.4 允许的差异
        .ToList();

    return new RegressionReport(
        RawAdded: rawDiff.Count(d => d.Op == DiffOp.Added),
        RawRemoved: rawDiff.Count(d => d.Op == DiffOp.Removed),
        ToleratedRemoved: toleratedDiff.Count(d => d.Op == DiffOp.Removed),
        RegressionCount: toleratedDiff.Count,    // > 0 = 回归
        Details: toleratedDiff
    );
}
```

### 3.6 零回归失败响应

| 回归级别 | 判定 | 响应 |
|----------|------|------|
| 0 容忍差异 | regression_count = 0 | ✅ Pass |
| 单文档容忍 | regression_count ≤ 1 | ⚠ 警告，不阻塞 |
| 多文档回归 | regression_count ≥ 2 | 🔴 阻塞发版 |
| 大面积回归 | regression_count ≥ 5 | 🔴 阻塞 PR + 紧急审查 |

---

## 4. 可执行测试代码骨架

> **重要**：本节**只产出骨架**（类签名、方法签名、注释占位），**不写完整业务实现**。人类工程师将根据 T05 任务清单接手实现。

### 4.1 新增项目文件结构

```
tests/Doc2MD.Converter.ScenarioTests/
├── Doc2MD.Converter.ScenarioTests.csproj    # 独立测试项目（无 WPF 依赖）
├── ScenarioTestBase.cs                      # 测试基类
├── Fixtures/                                # 30+ 脱敏文档样本（占位符）
│   ├── pdf/
│   ├── word/
│   ├── excel/
│   ├── ppt/
│   ├── txt/
│   ├── edge/
│   └── README.md                            # 脱敏规范
├── Scenarios/                               # 30+ YAML 场景定义
│   ├── reading/
│   ├── searching/
│   ├── copying/
│   ├── sharing/
│   ├── failure/
│   └── regression/
├── Drivers/                                 # 测试驱动
│   ├── ConversionDriver.cs                  # 调用 Core 转换 API
│   ├── FailureDiagnosisDriver.cs            # 验证失败分类
│   ├── HumanReadableDriver.cs               # 验证可读性指标
│   └── SharingDriver.cs                     # MD 文件分享场景
├── Metrics/                                 # 可观察指标采集器
│   ├── ReadabilityMetrics.cs
│   ├── CopyRetentionMetrics.cs
│   └── AiRegressionMetrics.cs
├── Reports/                                 # HTML 报告
│   └── report-template.html
├── Assertions/                              # 自定义断言库
│   ├── MarkdownAssertions.cs
│   └── ReadabilityScoreCalculator.cs
├── GoldenCorpus/                            # 50 条 AI 黄金用例
│   ├── ai-golden-001.md
│   └── ...
└── ScenarioRunner.cs                        # 场景执行器（xUnit 入口）
```

### 4.2 ScenarioTests.csproj

```xml
<Project Sdk="Microsoft.NET.Sdk">

  <PropertyGroup>
    <TargetFramework>net8.0</TargetFramework>
    <Nullable>enable</Nullable>
    <ImplicitUsings>enable</ImplicitUsings>
    <RollForward>LatestMajor</RollForward>
    <IsPackable>false</IsPackable>
    <IsTestProject>true</IsTestProject>
    <AssemblyName>Doc2MD.Converter.ScenarioTests</AssemblyName>
    <RootNamespace>Doc2MD.ScenarioTests</RootNamespace>
  </PropertyGroup>

  <ItemGroup>
    <!-- 测试运行时 -->
    <PackageReference Include="Microsoft.NET.Test.Sdk" Version="17.11.1" />
    <PackageReference Include="xunit" Version="2.9.2" />
    <PackageReference Include="xunit.runner.visualstudio" Version="2.8.2" />

    <!-- 断言库（场景测试专用，主项目仍用 Assert） -->
    <PackageReference Include="FluentAssertions" Version="6.12.0" />

    <!-- YAML 场景描述 -->
    <PackageReference Include="YamlDotNet" Version="15.1.0" />

    <!-- UI 自动化（仅 ScenarioTests，不污染主发布包） -->
    <PackageReference Include="FlaUI.UIA3" Version="4.0.0" />
    <PackageReference Include="FlaUI.Core" Version="4.0.0" />

    <!-- 性能基准 -->
    <PackageReference Include="BenchmarkDotNet" Version="0.13.12" />
  </ItemGroup>

  <ItemGroup>
    <ProjectReference Include="..\..\src\Doc2MD.Converter.Core\Doc2MD.Converter.Core.csproj" />
  </ItemGroup>

  <ItemGroup>
    <!-- 场景 YAML 复制到 bin -->
    <Content Include="Scenarios\**\*.yaml" CopyToOutputDirectory="PreserveNewest" />
    <!-- 夹具复制到 bin（不污染 fixtures 根） -->
    <Content Include="Fixtures\**\*" CopyToOutputDirectory="PreserveNewest" />
    <!-- 黄金用例复制到 bin -->
    <Content Include="GoldenCorpus\**\*" CopyToOutputDirectory="PreserveNewest" />
  </ItemGroup>

</Project>
```

### 4.3 ScenarioTestBase.cs（基类骨架）

```csharp
// =====================================================================
// 文件：tests/Doc2MD.Converter.ScenarioTests/ScenarioTestBase.cs
// 用途：所有场景测试的基类，封装 fixture 加载、隔离目录、指标收集
// 风格：参考现有 E2EPipelineTests（xUnit + FixturesDir 解析）
// =====================================================================

using System.Diagnostics;
using System.IO;
using Xunit;

namespace Doc2MD.ScenarioTests;

/// <summary>
/// 场景测试基类。
/// 职责：
///   1. 解析 fixtures 路径（与既有 E2EPipelineTests 风格一致）
///   2. 为每个场景创建独立隔离目录
///   3. 注入 Driver / Metric 依赖
/// </summary>
public abstract class ScenarioTestBase : IDisposable
{
    // ===== Arranged fixtures =====

    /// <summary>夹具根目录解析（output → source 二级 fallback）</summary>
    protected static readonly string FixturesDir = ResolveFixturesDir();

    /// <summary>每场景独立隔离目录（GUID 命名）</summary>
    protected string ScenarioTempDir { get; }

    /// <summary>场景执行耗时</summary>
    protected Stopwatch Stopwatch { get; } = new();

    /// <summary>指标收集器（子类按需注入）</summary>
    protected ScenarioMetricsCollector Metrics { get; } = new();

    // ===== Lifecycle =====

    protected ScenarioTestBase()
    {
        // Arrange: 创建隔离目录
        ScenarioTempDir = Path.Combine(
            Path.GetTempPath(),
            $"scenario_{GetType().Name}_{Guid.NewGuid():N}");
        Directory.CreateDirectory(ScenarioTempDir);
    }

    public virtual void Dispose()
    {
        // Cleanup: 保留 artifacts 以便排查，但记录路径
        TestContext.WriteLine($"[Cleanup] Scenario temp dir: {ScenarioTempDir}");
    }

    // ===== Helpers =====

    private static string ResolveFixturesDir()
    {
        // 1. Try output directory (if content was copied by build)
        var outputDir = Path.Combine(AppContext.BaseDirectory, "Fixtures");
        if (Directory.Exists(outputDir) && Directory.GetFiles(outputDir).Length > 0)
            return outputDir;

        // 2. Fall back to source directory (bin/Release/net8.0/ → ../../../../Fixtures/)
        var projectDir = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", ".."));
        return Path.Combine(projectDir, "Fixtures");
    }

    /// <summary>加载指定 fixture（copy to temp 隔离）</summary>
    protected string LoadFixture(string relativePath)
    {
        // Arrange: 解析源路径
        var sourcePath = Path.Combine(FixturesDir, relativePath);
        if (!File.Exists(sourcePath))
            throw new FileNotFoundException($"Fixture not found: {sourcePath}");

        // Act: 拷贝到隔离目录
        var fileName = Path.GetFileName(relativePath);
        var destPath = Path.Combine(ScenarioTempDir, fileName);
        File.Copy(sourcePath, destPath, overwrite: true);

        return destPath;
    }
}

/// <summary>指标收集器（场景测试专用，区别于单元测试的简单 Assert）</summary>
internal sealed class ScenarioMetricsCollector
{
    private readonly Dictionary<string, double> _metrics = new();

    public void Record(string name, double value) => _metrics[name] = value;
    public IReadOnlyDictionary<string, double> Snapshot() => _metrics;

    public void ExportJson(string path)
    {
        // TODO: 实现 JSON 导出（供 HTML 报告消费）
        throw new NotImplementedException("骨架：工程师实现 JSON 序列化");
    }
}
```

### 4.4 ReadingScenariosTests.cs（5+ [Fact] 骨架）

```csharp
// =====================================================================
// 文件：tests/Doc2MD.Converter.ScenarioTests/ReadingScenariosTests.cs
// 场景：SC-001 ~ SC-006 阅读维度
// 依赖：ConversionDriver / HumanReadableDriver
// =====================================================================

using Doc2MD.ScenarioTests.Drivers;
using Doc2MD.ScenarioTests.Assertions;
using FluentAssertions;
using Xunit;

namespace Doc2MD.ScenarioTests;

[Trait("Category", "Scenario")]
[Trait("Dimension", "Reading")]
public class ReadingScenariosTests : ScenarioTestBase
{
    private readonly ConversionDriver _converter;
    private readonly HumanReadableDriver _humanizer;

    public ReadingScenariosTests()
    {
        // Arrange: 实例化驱动（无 DI，骨架阶段直接 new）
        _converter = new ConversionDriver();
        _humanizer = new HumanReadableDriver();
    }

    /// <summary>SC-001：阅读-短文档TOC跳转</summary>
    [Fact]
    [Trait("Priority", "P0")]
    [Trait("Flaky", "false")]
    public async Task SC001_ShortPdf_TocJump_Succeeds()
    {
        // Arrange
        var pdfPath = LoadFixture("pdf/short_report_5pages.pdf");
        var scenario = ScenarioCatalog.SC001();

        // Act
        var mdPath = await _converter.ConvertToMdAsync(pdfPath, ScenarioTempDir);
        Stopwatch.Start();
        var tocResult = await _humanizer.EnableAndGetTocAsync(mdPath);
        Stopwatch.Stop();

        // Assert
        tocResult.Links.Should().HaveCount(5);
        Metrics.Record("toc_link_count", tocResult.Links.Count);
        Metrics.Record("step_jump_latency_ms", Stopwatch.ElapsedMilliseconds);

        // TODO 工程师实现：调用 MetricAssert 断言
        // MetricAssert.GreaterThan(tocSuccessRate, 0.95);
        // MetricAssert.LessThan(Stopwatch.ElapsedMilliseconds, 500);
    }

    /// <summary>SC-002：阅读-长文档TOC可用性</summary>
    [Fact]
    [Trait("Priority", "P0")]
    [Trait("Flaky", "false")]
    public async Task SC002_LongPdf_TocUsable()
    {
        // Arrange
        var pdfPath = LoadFixture("pdf/long_report_50pages.pdf");

        // Act
        var mdPath = await _converter.ConvertToMdAsync(pdfPath, ScenarioTempDir);
        var tocResult = await _humanizer.EnableAndGetTocAsync(mdPath);

        // Assert
        tocResult.Should().NotBeNull();
        tocResult.Links.Count.Should().BeGreaterThan(30);  // PRD 阈值
        MarkdownAssertions.ShouldHaveNoHeadingLevelJump(tocResult.EnhancedMd);
        // TODO: 工件师实现 TOC 字数统计断言
    }

    /// <summary>SC-003：阅读-Word大纲导航</summary>
    [Fact]
    [Trait("Priority", "P1")]
    public async Task SC003_WordOutline_Navigable()
    {
        // Arrange
        var docxPath = LoadFixture("word/outline_10pages.docx");

        // Act
        var mdPath = await _converter.ConvertToMdAsync(docxPath, ScenarioTempDir);

        // Assert
        var headingLevels = MarkdownAssertions.ExtractHeadingLevels(mdPath);
        headingLevels.Should().NotContain(l => l >= 4);  // 不超过 H3
        // TODO: 工件师实现大纲层级断言
    }

    /// <summary>SC-004：阅读-Excel多Sheet</summary>
    [Fact]
    [Trait("Priority", "P1")]
    public async Task SC004_ExcelMultiSheet_Navigable()
    {
        // Arrange
        var xlsxPath = LoadFixture("excel/multi_sheet_5.xlsx");

        // Act
        var mdPath = await _converter.ConvertToMdAsync(xlsxPath, ScenarioTempDir);

        // Assert
        MarkdownAssertions.ShouldContainSectionSeparators(mdPath, expectedCount: 5);
        // TODO: 工件师实现 Sheet 间跳转断言
    }

    /// <summary>SC-005：阅读-PPT幻灯片顺序</summary>
    [Fact]
    [Trait("Priority", "P1")]
    public async Task SC005_PptSlides_PreserveOrder()
    {
        // Arrange
        var pptxPath = LoadFixture("ppt/slides_20.pptx");

        // Act
        var mdPath = await _converter.ConvertToMdAsync(pptxPath, ScenarioTempDir);

        // Assert
        MarkdownAssertions.ShouldHaveSequentialSlides(mdPath, expectedCount: 20);
    }

    /// <summary>SC-006：阅读-超大PDF滚动（标记 Flaky）</summary>
    [Fact]
    [Trait("Priority", "P0")]
    [Trait("Flaky", "true")]
    public async Task SC006_HugePdf_NoUiFreeze()
    {
        // Arrange
        var pdfPath = LoadFixture("pdf/huge_100mb_500pages.pdf");

        // Act
        var sw = Stopwatch.StartNew();
        var mdPath = await _converter.ConvertToMdAsync(pdfPath, ScenarioTempDir);
        sw.Stop();

        // Assert
        Metrics.Record("conversion_wall_clock_ms", sw.ElapsedMilliseconds);
        // TODO: 工件师实现帧率/进度条平滑性断言（需 UI driver）
    }
}

/// <summary>场景目录（YAML 反序列化的内部表示，骨架阶段用硬编码常量）</summary>
internal static class ScenarioCatalog
{
    public static ScenarioInfo SC001() => new("SC-001", "阅读-短文档TOC跳转", Priority: "P0");
    // ... 其余 29 条类似 ...
}

internal sealed record ScenarioInfo(string Id, string Name, string Priority);
```

### 4.5 SearchingScenariosTests.cs（骨架）

```csharp
// =====================================================================
// 文件：tests/Doc2MD.Converter.ScenarioTests/SearchingScenariosTests.cs
// 场景：SC-007 ~ SC-011 查阅维度
// =====================================================================

using Doc2MD.ScenarioTests.Drivers;
using Doc2MD.ScenarioTests.Assertions;
using FluentAssertions;
using Xunit;

namespace Doc2MD.ScenarioTests;

[Trait("Category", "Scenario")]
[Trait("Dimension", "Searching")]
public class SearchingScenariosTests : ScenarioTestBase
{
    private readonly ConversionDriver _converter;

    public SearchingScenariosTests()
    {
        _converter = new ConversionDriver();
    }

    /// <summary>SC-007：查阅-单关键字定位</summary>
    [Fact]
    [Trait("Priority", "P0")]
    public async Task SC007_SingleKeyword_Locate()
    {
        // Arrange
        var pdfPath = LoadFixture("pdf/gov_document_request.pdf");
        var keyword = "请示";

        // Act
        var mdPath = await _converter.ConvertToMdAsync(pdfPath, ScenarioTempDir);
        var sw = Stopwatch.StartNew();
        var hits = MarkdownAssertions.SearchKeyword(mdPath, keyword);
        sw.Stop();

        // Assert
        hits.Should().NotBeEmpty();
        Metrics.Record("search_latency_ms", sw.ElapsedMilliseconds);
        Metrics.Record("search_recall", hits.Count);
        // TODO: MetricAssert.GreaterThan(sw.ElapsedMilliseconds, 2000); // 2s 阈值
    }

    [Fact]
    [Trait("Priority", "P0")]
    public async Task SC008_MultiKeyword_HighlightAll()
    {
        // Arrange
        var docxPath = LoadFixture("word/with_keyword_table.docx");
        var keywords = new[] { "实施", "计划", "预算" };

        // Act
        var mdPath = await _converter.ConvertToMdAsync(docxPath, ScenarioTempDir);

        // Assert
        foreach (var kw in keywords)
        {
            MarkdownAssertions.ShouldContainKeyword(mdPath, kw);
        }
        // TODO: 工件师实现召回率 ≥ 95% 断言
    }

    [Fact]
    [Trait("Priority", "P1")]
    public async Task SC009_DateRegex_Search() { /* 骨架占位 */ }

    [Fact]
    [Trait("Priority", "P1")]
    public async Task SC010_CrossSection_Search() { /* 骨架占位 */ }

    [Fact]
    [Trait("Priority", "P0")]
    public async Task SC011_ScannedPdf_NoKeyword_HonestPrompt()
    {
        // Arrange
        var pdfPath = LoadFixture("pdf/scanned_no_text.pdf");

        // Act
        var result = await _converter.ConvertToMdAsync(pdfPath, ScenarioTempDir);

        // Assert
        result.Should().NotBeNull();
        MarkdownAssertions.ShouldIndicateNoTextContent(result.OutputPath);
        // TODO: 工件师实现"明确提示需 OCR"断言
    }
}
```

### 4.6 CopyingScenariosTests.cs（骨架）

```csharp
// =====================================================================
// 文件：tests/Doc2MD.Converter.ScenarioTests/CopyingScenariosTests.cs
// 场景：SC-012 ~ SC-019 复制维度
// 依赖：CopyRetentionMetrics
// =====================================================================

using Doc2MD.ScenarioTests.Drivers;
using Doc2MD.ScenarioTests.Metrics;
using FluentAssertions;
using Xunit;

namespace Doc2MD.ScenarioTests;

[Trait("Category", "Scenario")]
[Trait("Dimension", "Copying")]
public class CopyingScenariosTests : ScenarioTestBase
{
    private readonly ConversionDriver _converter;
    private readonly CopyRetentionMetrics _retention;

    public CopyingScenariosTests()
    {
        _converter = new ConversionDriver();
        _retention = new CopyRetentionMetrics();
    }

    /// <summary>SC-012：复制-段落粘贴保留</summary>
    [Fact]
    [Trait("Priority", "P0")]
    public async Task SC012_ParagraphPaste_StructurePreserved()
    {
        // Arrange
        var pdfPath = LoadFixture("pdf/gov_document_request.pdf");

        // Act
        var mdPath = await _converter.ConvertToMdAsync(pdfPath, ScenarioTempDir);
        var paragraphRange = MarkdownAssertions.ExtractParagraph(mdPath, "正文");
        var simulatedPaste = _retention.SimulatePasteToWord(paragraphRange);

        // Assert
        var retentionRate = _retention.MeasureStructureRetention(paragraphRange, simulatedPaste);
        Metrics.Record("copy_retention_rate", retentionRate);
        retentionRate.Should().BeGreaterThan(0.95);
    }

    [Fact]
    [Trait("Priority", "P0")]
    public async Task SC013_TablePaste_RowColConsistent()
    {
        // Arrange
        var docxPath = LoadFixture("word/with_table_5x3.docx");

        // Act
        var mdPath = await _converter.ConvertToMdAsync(docxPath, ScenarioTempDir);
        var table = MarkdownAssertions.ExtractTable(mdPath, expectedRows: 5, expectedCols: 3);

        // Assert
        table.Rows.Count.Should().Be(5);
        table.Columns.Count.Should().Be(3);
    }

    [Fact]
    [Trait("Priority", "P0")]
    public async Task SC014_NestedList_3LevelsPreserved()
    {
        // Arrange
        var txtPath = LoadFixture("txt/nested_list_3levels.txt");

        // Act
        var mdPath = await _converter.ConvertToMdAsync(txtPath, ScenarioTempDir);

        // Assert
        var levels = MarkdownAssertions.ExtractListNestingDepth(mdPath);
        levels.Max().Should().Be(3);
    }

    [Fact]
    public async Task SC015_HeadingStyle_Retained() { /* 骨架 */ }

    [Fact]
    public async Task SC016_CodeBlock_ToVsCode() { /* 骨架 */ }

    [Fact]
    public async Task SC017_ImagePlaceholder_Retained() { /* 骨架 */ }

    [Fact]
    [Trait("Priority", "P0")]
    public async Task SC018_EncryptedFile_FriendlyError()
    {
        // Arrange
        var pdfPath = LoadFixture("pdf/encrypted_password.pdf");

        // Act
        var result = await _converter.ConvertToMdAsync(pdfPath, ScenarioTempDir);

        // Assert
        result.Success.Should().BeFalse();
        result.ErrorMessage.Should().NotContain("PdfDocumentEncryptedException");  // 不暴露堆栈
        result.ErrorMessage.Should().Contain("加密");  // 友好中文提示
    }

    [Fact]
    [Trait("Priority", "P0")]
    public async Task SC019_CorruptedFile_OcrFallback()
    {
        // Arrange
        var pdfPath = LoadFixture("pdf/corrupted_header.pdf");

        // Act
        var result = await _converter.ConvertToMdAsync(pdfPath, ScenarioTempDir);

        // Assert
        result.Should().NotBeNull();
        // TODO: 工件师实现 OCR 兜底成功 ≥ 70% 断言
    }
}
```

### 4.7 SharingScenariosTests.cs（骨架）

```csharp
// =====================================================================
// 文件：tests/Doc2MD.Converter.ScenarioTests/SharingScenariosTests.cs
// 场景：SC-020 ~ SC-023 分享维度
// 依赖：SharingDriver
// =====================================================================

using Doc2MD.ScenarioTests.Drivers;
using FluentAssertions;
using Xunit;

namespace Doc2MD.ScenarioTests;

[Trait("Category", "Scenario")]
[Trait("Dimension", "Sharing")]
public class SharingScenariosTests : ScenarioTestBase
{
    private readonly SharingDriver _sharing;

    public SharingScenariosTests()
    {
        _sharing = new SharingDriver();
    }

    /// <summary>SC-020：分享-邮件附件 MD</summary>
    [Fact]
    [Trait("Priority", "P1")]
    public async Task SC020_EmailAttachment_Md()
    {
        // Arrange
        var pdfPath = LoadFixture("pdf/long_report_50pages.pdf");

        // Act
        var mdPath = await _sharing.ConvertAndPackageForEmailAsync(pdfPath, ScenarioTempDir);

        // Assert
        File.Exists(mdPath).Should().BeTrue();
        var mdContent = File.ReadAllText(mdPath);
        mdContent.Should().Contain("## 目录");  // 人类路径含 TOC
    }

    [Fact]
    public async Task SC021_ImText_TyporaRender() { /* 骨架：模拟 IM 发送 */ }

    [Fact]
    public async Task SC022_ZipOutput_Integrity() { /* 骨架 */ }

    [Fact]
    public async Task SC023_EmptyFile_FriendlySkip() { /* 骨架 */ }
}
```

### 4.8 FailureDiagnosisScenariosTests.cs（骨架）

```csharp
// =====================================================================
// 文件：tests/Doc2MD.Converter.ScenarioTests/FailureDiagnosisScenariosTests.cs
// 场景：SC-024 ~ SC-028 失败处理维度
// 依赖：FailureDiagnosisDriver
// =====================================================================

using Doc2MD.Failures;  // 架构 §A.4 接口契约
using Doc2MD.ScenarioTests.Drivers;
using FluentAssertions;
using Xunit;

namespace Doc2MD.ScenarioTests;

[Trait("Category", "Scenario")]
[Trait("Dimension", "Failure")]
public class FailureDiagnosisScenariosTests : ScenarioTestBase
{
    private readonly FailureDiagnosisDriver _diagnosis;
    private readonly ConversionDriver _converter;

    public FailureDiagnosisScenariosTests()
    {
        _diagnosis = new FailureDiagnosisDriver();
        _converter = new ConversionDriver();
    }

    /// <summary>SC-024：失败-加密 PDF 分类 + UI 提示</summary>
    [Fact]
    [Trait("Priority", "P0")]
    public async Task SC024_EncryptedPdf_ClassifiedAndPrompted()
    {
        // Arrange
        var pdfPath = LoadFixture("pdf/encrypted_password.pdf");

        // Act
        var conversionResult = await _converter.ConvertToMdAsync(pdfPath, ScenarioTempDir);
        var diagnosis = await _diagnosis.DiagnoseAsync(conversionResult);

        // Assert
        diagnosis.PrimaryCategory.Should().Be(FailureCategory.SourceFileEncrypted);
        diagnosis.IsRetryable.Should().BeFalse();
        diagnosis.SuggestedAction.Should().Contain("解密");

        // TODO: 工件师实现 UI 徽章 + 跳过按钮断言（需 FlaUI driver）
    }

    [Fact]
    [Trait("Priority", "P0")]
    public async Task SC025_ScannedPdf_OcrFallback()
    {
        // Arrange
        var pdfPath = LoadFixture("pdf/scanned_no_text.pdf");

        // Act
        var result = await _converter.ConvertToMdAsync(pdfPath, ScenarioTempDir);

        // Assert
        result.Success.Should().BeTrue();  // OCR 兜底成功
        result.Warnings.Should().Contain(w => w.Contains("OCR"));
    }

    [Fact]
    [Trait("Priority", "P0")]
    public async Task SC026_LibreOfficeMissing_InstallButton()
    {
        // Arrange
        var docPath = LoadFixture("edge/legacy_word.doc");  // .doc 需 LibreOffice

        // Act
        var diagnosis = await _diagnosis.DiagnoseAsyncForMissingDependency(docPath);

        // Assert
        diagnosis.PrimaryCategory.Should().Be(FailureCategory.DependencyMissing);
        diagnosis.SuggestedAction.Should().Contain("LibreOffice");
        // TODO: 工件师实现 UI 弹窗 + 5s 内打开浏览器断言
    }

    [Fact]
    [Trait("Priority", "P0")]
    public async Task SC027_ResourceExhausted_DowngradeConcurrency()
    {
        // Arrange
        var manyPdfPaths = Directory.GetFiles(Path.Combine(FixturesDir, "pdf")).Take(200);

        // Act
        var result = await _converter.BatchConvertAsync(manyPdfPaths, ScenarioTempDir);

        // Assert
        result.FailedFiles.Should().BeEmpty();  // 自动降级，不 OOM
        // TODO: 工件师实现内存占用 < 阈值断言
    }

    [Fact]
    [Trait("Priority", "P0")]
    public async Task SC028_FileLocked_DetectAndPrompt()
    {
        // Arrange
        var docxPath = LoadFixture("edge/ppt_already_open.pptx");  // 模拟已打开
        // 用 FileShare.None 模拟占用
        using var lockHandle = new FileStream(docxPath, FileMode.Open, FileAccess.Read, FileShare.None);

        // Act
        var diagnosis = await _diagnosis.DiagnoseAsyncForLockedFile(docxPath);

        // Assert
        diagnosis.PrimaryCategory.Should().Be(FailureCategory.FileLocked);
        diagnosis.SuggestedAction.Should().Contain("关闭");
    }
}
```

### 4.9 HumanizerRegressionTests.cs（AI 零回归黄金用例骨架）

```csharp
// =====================================================================
// 文件：tests/Doc2MD.Converter.ScenarioTests/HumanizerRegressionTests.cs
// 场景：SC-029 ~ SC-030 AI 零回归
// 依赖：AiRegressionMetrics
// =====================================================================

using Doc2MD.ScenarioTests.Metrics;
using Doc2MD.ScenarioTests.Drivers;
using FluentAssertions;
using Xunit;

namespace Doc2MD.ScenarioTests;

[Trait("Category", "Scenario")]
[Trait("Dimension", "Regression")]
[Trait("GoldenCorpus", "true")]
public class HumanizerRegressionTests : ScenarioTestBase
{
    private static readonly string GoldenCorpusDir = ResolveGoldenCorpusDir();
    private readonly AiRegressionMetrics _regression;
    private readonly ConversionDriver _converter;
    private readonly HumanReadableDriver _humanizer;

    public HumanizerRegressionTests()
    {
        _regression = new AiRegressionMetrics();
        _converter = new ConversionDriver();
        _humanizer = new HumanReadableDriver();
    }

    /// <summary>SC-029：AI 路径零回归（50 条黄金用例）</summary>
    [Theory]
    [Trait("Priority", "P0")]
    [InlineData("ai-golden-001.md")]
    [InlineData("ai-golden-002.md")]
    // ... 其余 48 条（骨架示例仅列 2 条） ...
    [InlineData("ai-golden-050.md")]
    public async Task SC029_AiPath_ZeroRegression(string goldenFile)
    {
        // Arrange
        var goldenPath = Path.Combine(GoldenCorpusDir, goldenFile);
        var baselinePath = Path.Combine(GoldenCorpusDir, goldenFile.Replace(".md", ".expected.md"));

        // Act
        var inputMd = File.ReadAllText(goldenPath);
        var baselineMd = File.ReadAllText(baselinePath);
        var aiMd = await _converter.PostProcessAiPathAsync(inputMd);
        var report = _regression.Compare(baselineMd, aiMd);

        // Assert
        report.RegressionCount.Should().Be(0,
            $"AI 路径必须零回归。文档 {goldenFile} 回归项: {string.Join(", ", report.Details)}");
    }

    /// <summary>SC-030：人类路径增强（50 条黄金用例）</summary>
    [Theory]
    [Trait("Priority", "P0")]
    [InlineData("ai-golden-001.md")]
    // ... 其余 49 条 ...
    public async Task SC030_HumanPath_Enhanced(string goldenFile)
    {
        // Arrange
        var goldenPath = Path.Combine(GoldenCorpusDir, goldenFile);

        // Act
        var inputMd = File.ReadAllText(goldenPath);
        var humanMd = await _humanizer.EnableAndApplyAsync(inputMd);

        // Assert
        MarkdownAssertions.ShouldContainTocIfLong(humanMd);
        MarkdownAssertions.ShouldHaveNoHeadingLevelJump(humanMd);
        // TODO: 工件师实现增强项断言（TOC / 高亮 / 占位）
    }

    private static string ResolveGoldenCorpusDir()
    {
        var outputDir = Path.Combine(AppContext.BaseDirectory, "GoldenCorpus");
        if (Directory.Exists(outputDir) && Directory.GetFiles(outputDir).Any(f => f.EndsWith(".md")))
            return outputDir;

        var projectDir = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", ".."));
        return Path.Combine(projectDir, "GoldenCorpus");
    }
}
```

### 4.10 EdgeCaseScenariosTests.cs（边界场景骨架）

```csharp
// =====================================================================
// 文件：tests/Doc2MD.Converter.ScenarioTests/EdgeCaseScenariosTests.cs
// 场景：覆盖加密 / 损坏 / 扫描 / 超长 / 空文件 5 类边界
// 补充 Reading/Copying/Failure 中未覆盖的边界场景
// =====================================================================

using Doc2MD.ScenarioTests.Drivers;
using FluentAssertions;
using Xunit;

namespace Doc2MD.ScenarioTests;

[Trait("Category", "Scenario")]
[Trait("Dimension", "EdgeCase")]
public class EdgeCaseScenariosTests : ScenarioTestBase
{
    private readonly ConversionDriver _converter;

    public EdgeCaseScenariosTests()
    {
        _converter = new ConversionDriver();
    }

    /// <summary>边界：空文件不崩溃</summary>
    [Fact]
    [Trait("Priority", "P0")]
    public async Task EmptyFile_NoCrash_FriendlyPrompt()
    {
        // Arrange
        var emptyPath = LoadFixture("txt/empty_0bytes.txt");

        // Act
        var result = await _converter.ConvertToMdAsync(emptyPath, ScenarioTempDir);

        // Assert
        result.Should().NotBeNull();
        result.Success.Should().BeFalse();
        result.ErrorMessage.Should().Contain("空文件");
    }

    /// <summary>边界：超长文件不 OOM</summary>
    [Fact]
    [Trait("Priority", "P0")]
    [Trait("Flaky", "true")]
    public async Task HugeFile_NoOutOfMemory()
    {
        // Arrange
        var hugePath = LoadFixture("pdf/huge_100mb_500pages.pdf");

        // Act
        var result = await _converter.ConvertToMdAsync(hugePath, ScenarioTempDir);

        // Assert
        result.Should().NotBeNull();
        Metrics.Record("peak_memory_mb", GC.GetTotalMemory(false) / 1024 / 1024);
        // TODO: 工件师实现内存占用阈值断言
    }

    /// <summary>边界：损坏文件不假成功</summary>
    [Fact]
    public async Task CorruptedFile_NotFalselySucceeded() { /* 骨架 */ }

    /// <summary>边界：扫描 PDF 明确提示</summary>
    [Fact]
    public async Task ScannedPdf_HonestPrompt() { /* 骨架 */ }

    /// <summary>边界：加密文件友好错误</summary>
    [Fact]
    public async Task EncryptedFile_FriendlyError() { /* 骨架 */ }
}
```

### 4.11 Drivers（4 个驱动骨架）

#### 4.11.1 ConversionDriver.cs

```csharp
// =====================================================================
// 文件：tests/Doc2MD.Converter.ScenarioTests/Drivers/ConversionDriver.cs
// 用途：封装 Doc2MD Core 转换 API，场景测试专用入口
// 契约：与架构 §A.4 ConversionService 保持一致
// =====================================================================

using Doc2MD.Models;  // ConversionResult, ConversionMetadata

namespace Doc2MD.ScenarioTests.Drivers;

/// <summary>
/// 转换驱动。封装 Core 层的 ConversionService.ConvertFileAsync。
/// 场景测试不直接 new ConversionService（避免触发 DI），而是通过此 driver 间接调用。
/// </summary>
public sealed class ConversionDriver
{
    /// <summary>将源文件转换为 Markdown，输出到指定目录</summary>
    /// <param name="sourcePath">源文件（PDF/Word/Excel/PPT/TXT）</param>
    /// <param name="outputDir">输出目录（隔离）</param>
    /// <returns>转换结果（含 OutputPath / Success / ErrorMessage）</returns>
    public async Task<ConversionResult> ConvertToMdAsync(string sourcePath, string outputDir)
    {
        // TODO 工程师实现：
        //   1. 解析源文件 → IDocumentParser
        //   2. 调用 ConversionService.ConvertFileAsync
        //   3. 返回 ConversionResult
        throw new NotImplementedException("骨架：工程师实现 Core API 桥接");
    }

    /// <summary>AI 路径后处理（不经过 Humanizer）</summary>
    public async Task<string> PostProcessAiPathAsync(string rawMarkdown)
    {
        // TODO 工程师实现：
        //   调用 MarkdownPostProcessor.Process（v1.0.0 已有）
        throw new NotImplementedException("骨架：工程师实现 AI 路径");
    }

    /// <summary>批量转换</summary>
    public async Task<BatchConversionResult> BatchConvertAsync(
        IEnumerable<string> sourcePaths, string outputDir)
    {
        // TODO 工程师实现：
        //   调用 ConversionService.BatchConvert
        throw new NotImplementedException("骨架：工程师实现批量 API");
    }
}

public sealed record BatchConversionResult(
    int TotalFiles,
    int SucceededFiles,
    IReadOnlyList<string> FailedFiles,
    TimeSpan WallClock);
```

#### 4.11.2 FailureDiagnosisDriver.cs

```csharp
// =====================================================================
// 文件：tests/Doc2MD.Converter.ScenarioTests/Drivers/FailureDiagnosisDriver.cs
// 用途：调用架构 §A.4 FailureOrchestrator.HandleAsync，验证失败分类
// =====================================================================

using Doc2MD.Failures;
using Doc2MD.Models;

namespace Doc2MD.ScenarioTests.Drivers;

/// <summary>
/// 失败诊断驱动。封装架构 §A.4 FailureOrchestrator。
/// </summary>
public sealed class FailureDiagnosisDriver
{
    /// <summary>对转换失败结果进行诊断</summary>
    public async Task<FailureDiagnosis> DiagnoseAsync(ConversionResult result)
    {
        // TODO 工程师实现：
        //   调用 IFailureDiagnoser.Diagnose(result.LastException, result)
        throw new NotImplementedException("骨架：工程师实现诊断调用");
    }

    /// <summary>诊断缺失依赖场景</summary>
    public async Task<FailureDiagnosis> DiagnoseAsyncForMissingDependency(string legacyFilePath)
    {
        // TODO 工程师实现：
        //   模拟 LibreOffice 未安装场景，触发 DependencyMissing 诊断
        throw new NotImplementedException("骨架：工程师实现依赖缺失场景");
    }

    /// <summary>诊断文件被占用场景</summary>
    public async Task<FailureDiagnosis> DiagnoseAsyncForLockedFile(string filePath)
    {
        // TODO 工程师实现：
        //   用 FileShare.None 占用文件后尝试转换
        throw new NotImplementedException("骨架：工程师实现文件占用场景");
    }
}
```

#### 4.11.3 HumanReadableDriver.cs

```csharp
// =====================================================================
// 文件：tests/Doc2MD.Converter.ScenarioTests/Drivers/HumanReadableDriver.cs
// 用途：封装架构 §B.3 HumanizerMiddleware，验证可读性指标
// =====================================================================

namespace Doc2MD.ScenarioTests.Drivers;

public sealed record TocInfo(
    IReadOnlyList<string> Links,
    string EnhancedMd,
    bool Generated);

/// <summary>
/// 可读性优化驱动。封装 HumanizerMiddleware。
/// </summary>
public sealed class HumanReadableDriver
{
    /// <summary>启用 Humanizer 并提取 TOC</summary>
    public async Task<TocInfo> EnableAndGetTocAsync(string mdPath)
    {
        // TODO 工程师实现：
        //   1. 读取 MD
        //   2. 调用 HumanizerMiddleware.ApplyAsync(Enabled=true)
        //   3. 解析 TOC 块
        throw new NotImplementedException("骨架：工程师实现 TOC 提取");
    }

    /// <summary>启用 Humanizer 并获取增强后 MD</summary>
    public async Task<string> EnableAndApplyAsync(string rawMarkdown)
    {
        // TODO 工程师实现：
        //   调用 HumanizerMiddleware.ApplyAsync(rawMarkdown, Enabled=true)
        throw new NotImplementedException("骨架：工程师实现增强管道");
    }
}
```

#### 4.11.4 SharingDriver.cs

```csharp
// =====================================================================
// 文件：tests/Doc2MD.Converter.ScenarioTests/Drivers/SharingDriver.cs
// 用途：模拟 MD 文件分享场景（邮件、IM、压缩包）
// =====================================================================

namespace Doc2MD.ScenarioTests.Drivers;

/// <summary>
/// 分享驱动。模拟邮件附件 / IM 文本 / 网盘分享等场景。
/// </summary>
public sealed class SharingDriver
{
    /// <summary>转换并打包为邮件附件</summary>
    public async Task<string> ConvertAndPackageForEmailAsync(string sourcePath, string outputDir)
    {
        // TODO 工程师实现：
        //   1. 转换源文件为 MD
        //   2. 启用 Humanizer
        //   3. 输出到 outputDir
        throw new NotImplementedException("骨架：工程师实现邮件附件打包");
    }
}
```

### 4.12 Metrics（3 个采集器骨架）

#### 4.12.1 ReadabilityMetrics.cs

```csharp
// =====================================================================
// 文件：tests/Doc2MD.Converter.ScenarioTests/Metrics/ReadabilityMetrics.cs
// 用途：采集阅读维度的可观察指标
// =====================================================================

namespace Doc2MD.ScenarioTests.Metrics;

/// <summary>
/// 可读性指标采集器。
/// 指标：
///   - Flesch-Kincaid 阅读分数（英文）
///   - 字数密度（中文替代）
///   - 标题层级跳变次数
///   - TOC 链接有效率
/// </summary>
public sealed class ReadabilityMetrics
{
    public double MeasureFleschKincaid(string markdown)
    {
        // TODO 工程师实现：
        //   Flesch Reading Ease = 206.835 - 1.015 × (words/sentences) - 84.6 × (syllables/words)
        throw new NotImplementedException("骨架：工程师实现 FK 公式");
    }

    public double MeasureChineseDensity(string markdown)
    {
        // TODO 工程师实现：
        //   中文字符数 / 总字符数
        throw new NotImplementedException("骨架：工程师实现中文密度");
    }

    public int CountHeadingLevelJumps(string markdown)
    {
        // TODO 工程师实现：
        //   检测 H1→H3 等跳级
        throw new NotImplementedException("骨架：工程师实现跳级检测");
    }
}
```

#### 4.12.2 CopyRetentionMetrics.cs

```csharp
// =====================================================================
// 文件：tests/Doc2MD.Converter.ScenarioTests/Metrics/CopyRetentionMetrics.cs
// 用途：采集复制粘贴后的结构保留率
// =====================================================================

namespace Doc2MD.ScenarioTests.Metrics;

/// <summary>
/// 复制保留率指标采集器。
/// 指标：
///   - 段落结构保留率
///   - 表格行列数一致性
///   - 列表嵌套层级保留率
/// </summary>
public sealed class CopyRetentionMetrics
{
    /// <summary>模拟粘贴到 Word 后的 DOM</summary>
    public string SimulatePasteToWord(string markdown)
    {
        // TODO 工程师实现：
        //   1. 解析 MD 为 AST
        //   2. 转换为 Word OOXML
        //   3. 重新读出为 plain text
        throw new NotImplementedException("骨架：工程师实现模拟粘贴");
    }

    /// <summary>测量结构保留率</summary>
    public double MeasureStructureRetention(string source, string pasted)
    {
        // TODO 工程师实现：
        //   比较 AST 结构相似度
        throw new NotImplementedException("骨架：工程师实现 AST 对比");
    }
}
```

#### 4.12.3 AiRegressionMetrics.cs

```csharp
// =====================================================================
// 文件：tests/Doc2MD.Converter.ScenarioTests/Metrics/AiRegressionMetrics.cs
// 用途：检测 Humanizer 是否引入 AI 解析回归
// =====================================================================

namespace Doc2MD.ScenarioTests.Metrics;

public enum DiffOp { Added, Removed, Modified }

public sealed record DiffEntry(DiffOp Op, string Token, int Line);

public sealed record RegressionReport(
    int RawAdded,
    int RawRemoved,
    int ToleratedRemoved,
    int RegressionCount,
    IReadOnlyList<DiffEntry> Details);

/// <summary>
/// AI 回归指标采集器。计算 Humanizer 前后 token diff，应用容忍规则。
/// </summary>
public sealed class AiRegressionMetrics
{
    /// <summary>对比 baseline 与实际输出，生成回归报告</summary>
    public RegressionReport Compare(string expectedBaseline, string actualOutput)
    {
        // TODO 工程师实现：
        //   1. Tokenize（行级 + 词级）
        //   2. ComputeDiff
        //   3. ApplyToleratedRules（架构 §3.4 容忍差异集合）
        //   4. 返回 RegressionReport
        throw new NotImplementedException("骨架：工程师实现 diff 算法");
    }

    private IReadOnlyList<string> Tokenize(string markdown)
    {
        // TODO 工程师实现：
        //   按行 + Markdown 元素（标题/段落/代码块/表格行）切分
        throw new NotImplementedException("骨架：工程师实现 tokenize");
    }
}
```

### 4.13 自定义断言库

#### 4.13.1 MarkdownAssertions.cs

```csharp
// =====================================================================
// 文件：tests/Doc2MD.Converter.ScenarioTests/Assertions/MarkdownAssertions.cs
// 用途：场景测试专用的 Markdown 结构断言
// 风格：参考 FluentAssertions 链式 API + xUnit Assert
// =====================================================================

using FluentAssertions;

namespace Doc2MD.ScenarioTests.Assertions;

/// <summary>
/// Markdown 断言库。封装标题层级、段落空行、表格列对齐、图片占位、AI 兼容性等断言。
/// </summary>
public static class MarkdownAssertions
{
    /// <summary>标题层级无跳级</summary>
    public static void ShouldHaveNoHeadingLevelJump(string markdown)
    {
        // TODO 工程师实现：
        //   提取所有标题，检测 H1→H3、H2→H4 等跳级
        throw new NotImplementedException("骨架：工程师实现标题层级检查");
    }

    /// <summary>段落间有 1 个空行</summary>
    public static void ShouldHaveConsistentParagraphSpacing(string markdown)
    {
        // TODO 工程师实现
        throw new NotImplementedException("骨架：工程师实现段落间距检查");
    }

    /// <summary>表格列对齐正确（| :--- | 或 | ---: |）</summary>
    public static void ShouldHaveAlignedTableColumns(string markdown)
    {
        // TODO 工程师实现
        throw new NotImplementedException("骨架：工程师实现表格对齐检查");
    }

    /// <summary>图片占位完整</summary>
    public static void ShouldHaveImagePlaceholders(string markdown, int expectedCount)
    {
        // TODO 工程师实现
        throw new NotImplementedException("骨架：工程师实现图片占位检查");
    }

    /// <summary>AI 兼容性（关键 token 保留）</summary>
    public static void ShouldBeAiCompatible(string originalMd, string enhancedMd)
    {
        // TODO 工程师实现：
        //   关键 token（标题文本、表格内容、代码块内容）一致
        throw new NotImplementedException("骨架：工程师实现 AI 兼容性检查");
    }

    // ===== 查询方法 =====

    public static IReadOnlyList<int> ExtractHeadingLevels(string mdPath)
    {
        // TODO 工程师实现
        throw new NotImplementedException("骨架：工程师实现标题层级提取");
    }

    public static string ExtractParagraph(string mdPath, string keyword)
    {
        // TODO 工程师实现
        throw new NotImplementedException("骨架：工程师实现段落提取");
    }

    public static TableInfo ExtractTable(string mdPath, int expectedRows, int expectedCols)
    {
        // TODO 工程师实现
        throw new NotImplementedException("骨架：工程师实现表格提取");
    }

    public static IReadOnlyList<int> ExtractListNestingDepth(string mdPath)
    {
        // TODO 工程师实现
        throw new NotImplementedException("骨架：工程师实现列表嵌套深度提取");
    }

    public static IReadOnlyList<int> SearchKeyword(string mdPath, string keyword)
    {
        // TODO 工程师实现
        throw new NotImplementedException("骨架：工程师实现关键字搜索");
    }

    public static void ShouldContainKeyword(string mdPath, string keyword) { /* 骨架 */ }

    public static void ShouldContainSectionSeparators(string mdPath, int expectedCount) { /* 骨架 */ }

    public static void ShouldHaveSequentialSlides(string mdPath, int expectedCount) { /* 骨架 */ }

    public static void ShouldContainTocIfLong(string markdown) { /* 骨架 */ }

    public static void ShouldIndicateNoTextContent(string mdPath) { /* 骨架 */ }
}

public sealed record TableInfo(IReadOnlyList<string> Rows, IReadOnlyList<string> Columns);
```

#### 4.13.2 ReadabilityScoreCalculator.cs

```csharp
// =====================================================================
// 文件：tests/Doc2MD.Converter.ScenarioTests/Assertions/ReadabilityScoreCalculator.cs
// 用途：基于 Flesch-Kincaid（英文）/ 字数密度（中文）的可读性评分
// =====================================================================

namespace Doc2MD.ScenarioTests.Assertions;

/// <summary>
/// 可读性评分计算器。
///   - 英文：Flesch-Kincaid Grade Level
///   - 中文：字数密度 + 平均句长
/// </summary>
public sealed class ReadabilityScoreCalculator
{
    /// <summary>计算英文可读性分数（FKGL）</summary>
    /// <returns>年级水平（越低越易读）</returns>
    public double CalculateEnglish(string markdown)
    {
        // TODO 工程师实现：
        //   FKGL = 0.39 × (words/sentences) + 11.8 × (syllables/words) - 15.59
        throw new NotImplementedException("骨架：工程师实现 FKGL");
    }

    /// <summary>计算中文可读性分数</summary>
    /// <returns>综合分数（0-100，越高越易读）</returns>
    public double CalculateChinese(string markdown)
    {
        // TODO 工程师实现：
        //   综合：字数密度 + 平均句长 + 章节标题数
        throw new NotImplementedException("骨架：工程师实现中文评分");
    }

    /// <summary>自动检测语言并计算</summary>
    public double AutoDetectAndCalculate(string markdown)
    {
        // TODO 工程师实现：
        //   中文占比 > 50% 用 CalculateChinese，否则 CalculateEnglish
        throw new NotImplementedException("骨架：工程师实现自动检测");
    }
}
```

### 4.14 ScenarioRunner.cs（场景执行器骨架）

```csharp
// =====================================================================
// 文件：tests/Doc2MD.Converter.ScenarioTests/ScenarioRunner.cs
// 用途：替代 xUnit 默认 TestRunner，提供 HTML 报告 + YAML 驱动
// 说明：xUnit 已提供 [Fact]/[Theory] 入口，本类用于：
//   1. 加载 YAML 场景定义
//   2. 串/并行执行
//   3. 输出 HTML 报告
// =====================================================================

using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

namespace Doc2MD.ScenarioTests;

/// <summary>
/// 场景执行器。支持 YAML 驱动、HTML 报告、Flaky 标记。
/// </summary>
public sealed class ScenarioRunner
{
    private readonly IDeserializer _yamlDeserializer;

    public ScenarioRunner()
    {
        _yamlDeserializer = new DeserializerBuilder()
            .WithNamingConvention(CamelCaseNamingConvention.Instance)
            .IgnoreUnmatchedProperties()
            .Build();
    }

    /// <summary>从 YAML 文件加载场景定义</summary>
    public ScenarioDefinition LoadScenario(string yamlPath)
    {
        // TODO 工程师实现：
        //   1. 读 YAML
        //   2. 反序列化为 ScenarioDefinition
        throw new NotImplementedException("骨架：工程师实现 YAML 加载");
    }

    /// <summary>执行场景并生成报告</summary>
    public async Task<ScenarioExecutionReport> RunAsync(ScenarioDefinition scenario)
    {
        // TODO 工程师实现：
        //   1. Setup（fixture 隔离）
        //   2. Execute（按步骤执行 actions）
        //   3. Assert（应用 success_criteria）
        //   4. Teardown
        //   5. 返回报告
        throw new NotImplementedException("骨架：工程师实现执行器");
    }

    /// <summary>批量执行多个场景</summary>
    public async Task<ScenarioExecutionReport> RunBatchAsync(IEnumerable<string> scenarioPaths)
    {
        // TODO 工程师实现：
        //   1. 加载所有 YAML
        //   2. 按 priority 分组（P0 先跑）
        //   3. 串/并行执行（Flaky 标记的串行）
        //   4. 汇总报告
        throw new NotImplementedException("骨架：工程师实现批量执行");
    }
}

public sealed record ScenarioDefinition(
    string Id,
    string Name,
    string UserGoal,
    string Priority,
    string Category,
    Preconditions Preconditions,
    IReadOnlyList<ScenarioAction> Actions,
    SuccessCriteria SuccessCriteria,
    EvidenceSpec Evidence);

public sealed record Preconditions(
    IReadOnlyList<FixtureSpec> Fixtures,
    IReadOnlyList<string> AppState);

public sealed record FixtureSpec(string Path, string Sha256);

public sealed record ScenarioAction(int Step, string Actor, string Action, string Expected);

public sealed record SuccessCriteria(
    string Metric,
    string Target,
    IReadOnlyDictionary<string, string>? Timing,
    IReadOnlyList<string>? OutputArtifacts);

public sealed record EvidenceSpec(
    IReadOnlyList<string> Screenshots,
    IReadOnlyList<string> Logs,
    string? MetricsJson);

public sealed record ScenarioExecutionResult(
    string ScenarioId,
    bool Passed,
    TimeSpan WallClock,
    string? FailureReason);

public sealed class ScenarioExecutionReport
{
    public DateTime StartTime { get; init; }
    public DateTime EndTime { get; set; }
    public List<ScenarioExecutionResult> Results { get; } = new();

    public int TotalCount => Results.Count;
    public int PassedCount => Results.Count(r => r.Passed);
    public double PassRate => TotalCount == 0 ? 0 : (double)PassedCount / TotalCount;

    public Task ExportHtmlAsync(string outputPath)
    {
        // TODO 工程师实现：
        //   渲染到 report-template.html
        throw new NotImplementedException("骨架：工程师实现 HTML 导出");
    }
}
```

---

## 5. CI 集成（GitHub Actions 骨架）

### 5.1 .github/workflows/scenario-tests.yml

```yaml
# =====================================================================
# 文件：.github/workflows/scenario-tests.yml
# 用途：场景测试 CI 工作流
# 触发：PR / 主干合并 / 发版前 / 每日定时
# =====================================================================

name: Scenario Tests

on:
  pull_request:
    branches: [main]
    paths:
      - 'src/**'
      - 'tests/Doc2MD.Converter.ScenarioTests/**'
      - '.github/workflows/scenario-tests.yml'
  push:
    branches: [main]
    paths:
      - 'src/**'
      - 'tests/Doc2MD.Converter.ScenarioTests/**'
  schedule:
    - cron: '0 2 * * *'  # 每日凌晨 2 点
  workflow_dispatch:        # 手动触发

env:
  DOTNET_VERSION: '8.0.x'

jobs:
  # ============================================================
  # Job 1: 单元测试 + 黄金场景（PR 必跑）
  # ============================================================
  pr-smoke:
    name: PR Smoke (Unit + Golden Scenarios)
    runs-on: windows-latest
    timeout-minutes: 10

    steps:
      - uses: actions/checkout@v4
        with:
          lfs: true  # 拉取大文件 fixture

      - name: Setup .NET
        uses: actions/setup-dotnet@v4
        with:
          dotnet-version: ${{ env.DOTNET_VERSION }}

      - name: Restore
        run: dotnet restore

      - name: Build
        run: dotnet build --configuration Release --no-restore

      - name: Run Unit Tests
        run: dotnet test tests/Doc2MD.Converter.Core.Tests --configuration Release --no-build --logger "trx;LogFileName=unit-tests.trx"

      - name: Run Golden Scenarios (P0 only)
        run: |
          dotnet test tests/Doc2MD.Converter.ScenarioTests --configuration Release --no-build \
            --filter "Category=Scenario&Priority=P0" \
            --logger "trx;LogFileName=scenario-tests.trx"

      - name: Upload Test Results
        if: always()
        uses: actions/upload-artifact@v4
        with:
          name: pr-test-results
          path: |
            **/*.trx
            **/scenario-report.html

  # ============================================================
  # Job 2: 全部场景测试（主干 + 发版前）
  # ============================================================
  full-scenarios:
    name: Full Scenarios (All 30+)
    runs-on: windows-latest
    timeout-minutes: 30
    needs: pr-smoke
    if: github.event_name == 'push' || github.event_name == 'schedule' || github.event_name == 'workflow_dispatch'

    steps:
      - uses: actions/checkout@v4
        with:
          lfs: true

      - name: Setup .NET
        uses: actions/setup-dotnet@v4
        with:
          dotnet-version: ${{ env.DOTNET_VERSION }}

      - name: Restore
        run: dotnet restore

      - name: Build
        run: dotnet build --configuration Release --no-restore

      - name: Run All Scenarios
        id: run-scenarios
        run: |
          dotnet test tests/Doc2MD.Converter.ScenarioTests --configuration Release --no-build \
            --filter "Category=Scenario" \
            --logger "trx;LogFileName=scenarios.trx" \
            --logger "console;verbosity=detailed" \
            2>&1 | tee scenario-output.log
        continue-on-error: true  # 收集报告但不中断

      - name: Check Pass Rate
        run: |
          $passRate = (Get-Content scenario-output.log | Select-String "Pass Rate: (\d+\.\d+)").Matches.Groups[1].Value
          if ([double]$passRate -lt 0.90) {
            Write-Error "场景通过率 $passRate < 0.90，发版阻塞"
            exit 1
          }

      - name: Upload HTML Report
        if: always()
        uses: actions/upload-artifact@v4
        with:
          name: scenario-report-${{ github.run_number }}
          path: |
            tests/Doc2MD.Converter.ScenarioTests/Reports/scenario-report.html
            tests/Doc2MD.Converter.ScenarioTests/Reports/scenario-report-*.html
            **/*.trx
          retention-days: 30

  # ============================================================
  # Job 3: AI 零回归（每日 + 发版前）
  # ============================================================
  ai-regression:
    name: AI Zero Regression (50 Golden)
    runs-on: windows-latest
    timeout-minutes: 15
    needs: pr-smoke
    if: github.event_name == 'push' || github.event_name == 'schedule' || github.event_name == 'workflow_dispatch'

    steps:
      - uses: actions/checkout@v4
        with:
          lfs: true

      - name: Setup .NET
        uses: actions/setup-dotnet@v4
        with:
          dotnet-version: ${{ env.DOTNET_VERSION }}

      - name: Restore & Build
        run: |
          dotnet restore
          dotnet build --configuration Release --no-restore

      - name: Run AI Regression (50 Golden)
        id: run-regression
        run: |
          dotnet test tests/Doc2MD.Converter.ScenarioTests --configuration Release --no-build \
            --filter "Dimension=Regression" \
            --logger "trx;LogFileName=ai-regression.trx"
        continue-on-error: true

      - name: Check Regression
        run: |
          $regression = (Get-Content ai-regression-output.log | Select-String "RegressionCount: (\d+)").Matches.Groups[1].Value
          if ([int]$regression -gt 5) {
            Write-Error "AI 回归数 $regression > 5，发版阻塞"
            exit 1
          }
```

### 5.2 Flaky 标记策略

| 标记 | 行为 | 适用场景 |
|------|------|----------|
| `[Trait("Flaky", "false")]`（默认） | 失败 = 阻塞 | 单元测试、P0 黄金场景 |
| `[Trait("Flaky", "true")]` | 失败 = 告警，不阻塞 | UI 自动化、性能基准、CI 头less |

**Flaky 用例管理规则**：
1. **每个 Flaky 用例必须有 owner**：注释中注明负责工程师
2. **Flaky 用例必须 7 天内 fix**：超时升级为 P0 bug
3. **Flaky 用例会单独跑**：不在 PR 必跑流程中

### 5.3 HTML 报告归档

CI 每次运行后，scenario-report.html 自动上传到 GitHub Artifacts，**保留 30 天**。开发者可：
- 从 Artifacts 下载最新报告
- 通过 `gh run view <run-id> --log` 查看历史
- 报告含失败 trace + 截图（如有）

---

## 6. 测试报告模板（HTML）

### 6.1 报告结构

```
┌─────────────────────────────────────────────────────────────────────┐
│  Doc2MD-Converter 场景测试报告                                          │
│  运行时间：2026-09-14 14:32:01 ~ 14:47:23  |  耗时：15m 22s          │
├─────────────────────────────────────────────────────────────────────┤
│  📊 总览                                                               │
│  ┌──────────┬──────────┬──────────┬──────────┬──────────┐           │
│  │ 总场景 30 │ 通过 27  │ 失败 3   │ 跳过 0   │ 通过率90% │           │
│  └──────────┴──────────┴──────────┴──────────┴──────────┘           │
├─────────────────────────────────────────────────────────────────────┤
│  📈 维度分布                                                            │
│  ┌──────────────────────────────────────────────────────────┐       │
│  │  阅读  ████████████████████ 6/6   (100%)                │       │
│  │  查阅  ████████████████     5/5   (100%)                │       │
│  │  复制  ██████████████       6/8   ( 75%) ⚠              │       │
│  │  分享  ████████████         4/4   (100%)                │       │
│  │  失败  ██████████████████   4/5   ( 80%)                │       │
│  │  回归  ████████████████     2/2   (100%)                │       │
│  └──────────────────────────────────────────────────────────┘       │
├─────────────────────────────────────────────────────────────────────┤
│  🎯 体验指标（KPI）                                                     │
│  ┌──────────────────────────────────────────────────────────┐       │
│  │  复制保留率：93.2%  (目标 ≥ 90%)  ✅                       │       │
│  │  AI 零回归：0 处   (目标 ≤ 5 处) ✅                        │       │
│  │  TOC 可用性：96.7% (目标 ≥ 95%) ✅                        │       │
│  │  首问解决率：82.5% (目标 ≥ 80%) ✅                        │       │
│  │  场景平均耗时：8.2s (目标 ≤ 30s) ✅                        │       │
│  └──────────────────────────────────────────────────────────┘       │
├─────────────────────────────────────────────────────────────────────┤
│  ⚠ 失败案例（3 条）                                                    │
│  ┌──────────────────────────────────────────────────────────┐       │
│  │ SC-013 复制-表格行列一致                                    │       │
│  │   失败步骤：step 3（验证表格）                              │       │
│  │   期望：表格 5×3                                            │       │
│  │   实际：表格 5×2（最后一列被截断）                           │       │
│  │   截图：step3-failure.png                                  │       │
│  │   Trace：[Scenario] SC-013 step=3 elapsed=230ms status=fail│      │
│  │                                                            │       │
│  │ SC-019 复制-损坏文件兜底                                    │       │
│  │   ...                                                      │       │
│  └──────────────────────────────────────────────────────────┘       │
├─────────────────────────────────────────────────────────────────────┤
│  🔁 AI 零回归对比快照                                                  │
│   v1.1.0-rc1 vs v1.1.0-rc2                                            │
│   [下载 baseline diff (50 个文件)](golden-diff.html)                  │
└─────────────────────────────────────────────────────────────────────┘
```

### 6.2 模板实现位置

HTML 模板完整代码见 `tests/Doc2MD.Converter.ScenarioTests/Reports/report-template.html`。模板要点：

1. **场景维度分组展示**：每维度一卡片，含通过率条形图（CSS 进度条）
2. **体验指标可视化**：KPI 数字 + 颜色（绿/黄/红）+ 与基线对比箭头
3. **失败案例高亮**：失败步骤展开 + 截图链接 + Trace
4. **AI 零回归对比快照**：链接到 `golden-diff.html`，含 50 个文件的 token diff 汇总

---

## 7. 附录

### 7.1 测试覆盖矩阵

| 文档类型 | 阅读 | 查阅 | 复制 | 分享 | 失败 | 边界 | 合计 |
|----------|------|------|------|------|------|------|------|
| PDF      | 3    | 4    | 5    | 1    | 3    | 2    | 18   |
| Word     | 1    | 1    | 2    | 1    | 1    | 1    | 7    |
| Excel    | 1    | 0    | 0    | 0    | 0    | 0    | 1    |
| PPT      | 1    | 0    | 0    | 0    | 1    | 0    | 2    |
| TXT      | 0    | 0    | 1    | 1    | 0    | 1    | 3    |
| 混合/其他| 0    | 0    | 0    | 1    | 0    | 1    | 2    |
| MD（黄金）| 0    | 0    | 0    | 0    | 0    | 2    | 2    |
| **合计** | **6**| **5**| **8**| **4**| **5**| **7**| **35**|

> 含 `EdgeCaseScenariosTests` 补充场景后总数 **35 条**，满足 PRD REQ-T-01 "≥ 30 条"。

### 7.2 关键可量化体验指标（4-6 个）

| # | 指标 | 目标 | 测量方式 |
|---|------|------|----------|
| 1 | **失败首问解决率** | ≥ 80% | `FailureLibraryViewModel.ResolutionRate` |
| 2 | **复制结构保留率** | ≥ 90% | `CopyRetentionMetrics.MeasureStructureRetention` |
| 3 | **AI 解析回归数** | 0（容忍 ≤ 5） | `AiRegressionMetrics.Compare` |
| 4 | **TOC 可用性** | ≥ 95% | `HumanReadableDriver.EnableAndGetTocAsync` + 点击验证 |
| 5 | **场景通过率** | ≥ 90% | `ScenarioRunner.RunBatchAsync` 汇总 |
| 6 | **场景平均耗时** | ≤ 30s | `Stopwatch` 包裹 `ScenarioContext.ExecuteAsync` |

### 7.3 与 PRD §6 验收门槛的对应

| 验收门槛 | 本设计的保证 |
|----------|-------------|
| 单元测试 295+ + 新增 ≥ 100 用例 100% 通过 | 既有 15 文件 + T05 新增 12 文件（架构 §C） |
| AI 解析回归 100% | 50 条黄金用例 + `AiRegressionMetrics` |
| 真实场景测试 30 条通过率 ≥ 90% | SC-001 ~ SC-030 + EdgeCase 补充（35 条） |
| 8 类失败均有诊断 + 策略 | `FailureDiagnosisScenariosTests` 覆盖 5 类 + 单元测试覆盖全部 |
| 可读性 N 步任务完成率 ≥ 90% | `ReadingScenariosTests` SC-001/002 断言 |
| 性能无退化 > 5% | BenchmarkDotNet + 场景耗时断言 |

### 7.4 与架构 §C 的对应

| 架构设计 | 本测试设计 |
|----------|-----------|
| Scenario 抽象 + YAML 驱动 | `ScenarioRunner.LoadScenario` + `ScenarioDefinition` |
| Page Object（轻量） | `Drivers/*.cs`（4 个驱动封装 UI/API） |
| 双轨管线（AI vs 人类） | `HumanizerRegressionTests` SC-029/030 验证零回归 |
| 8 类失败分类 | `FailureDiagnosisScenariosTests` SC-024~028 |
| CI 报告 HTML + JUnit XML | `report-template.html` + `--logger "trx"` |

### 7.5 修订记录

| 版本 | 日期 | 修订人 | 说明 |
|------|------|--------|------|
| v1.0 | 2026-09-14 | 严过关（Yan） | 首版基于 PRD + 架构设计输出 |

---

## 8. 工程师接手清单（Human Engineer TODO）

> 本文档交付给工程师后，**人类工程师需要补充的具体事项**：

### 8.1 必须由工程师完成（生产代码）

| 任务 | 来源 | 说明 |
|------|------|------|
| 实现 8 类失败枚举 + 元数据 | 架构 §A.2.1 | `FailureCategory.cs` |
| 实现 6 个重试策略 | 架构 §A.2.1 | `Strategies/*.cs` |
| 实现 `FailureOrchestrator` + `RetryOrchestrator` | 架构 §A.4.1 | 调度逻辑 |
| 实现 `HumanizerMiddleware` + 8 个 transform | 架构 §B.2 | 管道 + 链式 |
| 实现 `ConversionResult.FailureContext` 可选属性 | 架构 §0.2 | 不破坏现有契约 |
| 实现 `AppConfig` 新增 2 个子对象 | 架构 §3.4 | `HumanReadableSettings` + `FailureHandlingSettings` |

### 8.2 必须由工程师完成（测试代码填充）

> 本设计只产出**骨架**，工程师需填充以下方法的实现：

| 类 | 方法 | 优先级 |
|----|------|--------|
| `ConversionDriver` | `ConvertToMdAsync` | P0 |
| `ConversionDriver` | `PostProcessAiPathAsync` | P0 |
| `ConversionDriver` | `BatchConvertAsync` | P0 |
| `FailureDiagnosisDriver` | `DiagnoseAsync` | P0 |
| `FailureDiagnosisDriver` | `DiagnoseAsyncForMissingDependency` | P0 |
| `FailureDiagnosisDriver` | `DiagnoseAsyncForLockedFile` | P0 |
| `HumanReadableDriver` | `EnableAndGetTocAsync` | P0 |
| `HumanReadableDriver` | `EnableAndApplyAsync` | P0 |
| `SharingDriver` | `ConvertAndPackageForEmailAsync` | P1 |
| `ReadabilityMetrics` | `MeasureFleschKincaid` | P2 |
| `ReadabilityMetrics` | `MeasureChineseDensity` | P2 |
| `ReadabilityMetrics` | `CountHeadingLevelJumps` | P1 |
| `CopyRetentionMetrics` | `SimulatePasteToWord` | P1 |
| `CopyRetentionMetrics` | `MeasureStructureRetention` | P1 |
| `AiRegressionMetrics` | `Compare` | P0 |
| `AiRegressionMetrics` | `Tokenize` | P0 |
| `MarkdownAssertions` | 全部方法 | P0 |
| `ReadabilityScoreCalculator` | 全部方法 | P2 |
| `ScenarioRunner` | `LoadScenario` | P0 |
| `ScenarioRunner` | `RunAsync` | P0 |
| `ScenarioRunner` | `RunBatchAsync` | P0 |
| `ScenarioExecutionReport` | `ExportHtmlAsync` | P0 |
| `ScenarioMetricsCollector` | `ExportJson` | P1 |

### 8.3 必须由工程师/数据团队完成（夹具准备）

| 任务 | 数量 | 说明 |
|------|------|------|
| 准备 PDF 夹具 | 10 份 | 覆盖 5 类型 + 5 边界 |
| 准备 Word 夹具 | 2 份 | 含大纲、表格 |
| 准备 Excel 夹具 | 1 份 | 多 Sheet |
| 准备 PPT 夹具 | 1 份 | 20 张幻灯片 |
| 准备 TXT 夹具 | 4 份 | 含代码块、嵌套列表 |
| 准备边界夹具 | 3 份 | 加密/损坏/扫描/超长 |
| 准备 50 条 AI 黄金用例 | 50 份 | 基于 v1.0.0 ConversionQuality ≥ 0.9 文档 |

### 8.4 必须由工程师/QA 完成（YAML 场景定义）

| 任务 | 数量 | 说明 |
|------|------|------|
| 编写 SC-001 ~ SC-030 YAML | 30 份 | 严格遵循 §2.2 规范 |
| 编写 EdgeCase 补充 YAML | 5 份 | 覆盖 5 类边界 |

### 8.5 必须由 DevOps 完成（CI 集成）

| 任务 | 说明 |
|------|------|
| 接入 .github/workflows/scenario-tests.yml | 三 Job（PR / 全量 / 回归） |
| 配置 Artifacts 上传策略 | 保留 30 天 |
| 配置定时任务 | 每日凌晨 2 点跑全量 |
| 配置告警通知 | Slack / Email / GitHub Issues |

---

*测试体系设计文档结束。下一步：交付给人类工程师按 §8 清单接手实现，并行启动 §7.1 覆盖矩阵的夹具准备工作。*
