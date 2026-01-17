# Gemini Project Memory & Guidelines

## 🧠 核心指令 (Core Mandates)
- **语言偏好**: 所有思考过程 (Thinking Process) 和输出解释必须使用 **中文**。
- **操作权限**: 自动执行非破坏性操作 (读取、搜索、运行非修改类命令)。删除或破坏性修改需用户明确确认。

## 📍 项目上下文 (Context)
- **项目名称**: Ryujinx (M1/Apple Silicon Optimization)
- **当前目标**: 执行 `Ryujinx_M1_Optimization_Guide.md` 中的优化路线图，重点解决 8GB 内存设备的性能问题。
- **核心文档**:
    - `docs/M1_OPTIMIZATION_TODO.md`: 实时任务追踪清单。
    - `Ryujinx_M1_Optimization_Guide.md`: 原始技术指导书。

## ⚙️ 工作流规范 (Workflow Protocols)

### 1. 任务管理 (Task Management)
- **单一职责**: 每次只处理 `docs/M1_OPTIMIZATION_TODO.md` 中的一项具体任务。
- **状态同步**: 任务开始前标记为 `[IN PROGRESS]`，完成后标记为 `[x]`。

### 2. Git 分支策略 (Git Branching)
- **主分支**: `master` (或 `main`) - 保持随时可编译、可运行的状态。
- **特性分支**: 开发新功能或优化时，**必须**检出新分支。
    - 命名规范: `feature/<task-id>-<short-description>`
    - 示例: `feature/1.1-net9-upgrade`, `feature/1.2-astc-passthrough`
- **提交信息**:
    - 格式: `[模块] 说明 (为什么做，而不是做了什么)`
    - 示例: `[Gpu] Implement ASTC passthrough to reduce memory usage`

### 3. 存档点机制 (Save Points)
- **定义**: 在进行高风险或复杂修改过程中，为了防止代码崩坏，建立的临时稳定状态。
- **触发时机**:
    - 在开始大规模重构前。
    - 在完成一个子功能并通过编译时。
- **操作**: 使用 Git 提交，并在消息中包含 `[SAVE POINT]` 标记。
    - `git commit -am "[SAVE POINT] Before refactoring texture decoder"`

### 4. 质量验收 (QA & Verification)
- **编译检查**: 每次修改后必须通过 `dotnet build`。
- **无头测试**: 涉及核心逻辑修改，运行 `Ryujinx.Headless` 进行冒烟测试。
- **黄金镜像**: 图形渲染修改必须通过黄金镜像对比 (Golden Image Comparison)，确保无视觉回归。

### 5. 特性交付与打包验证 (Delivery & Verification Protocol)
在完成主要特性开发后，**必须**构建可独立运行的 macOS App Bundle 以进行最终验证。

**标准打包流程 (Shell):**
```bash
# 1. 定义变量
FEATURE_NAME="Feature-Name-Here"
PUBLISH_DIR="release_output/publish"
OUTPUT_DIR="release_output/$FEATURE_NAME"
APP_DIR="$OUTPUT_DIR/Ryujinx.app"

# 2. 构建发布 (OSX-ARM64)
dotnet publish -c Release -r osx-arm64 --self-contained true -o "$PUBLISH_DIR" src/Ryujinx/Ryujinx.csproj

# 3. 组装应用包
rm -rf "$APP_DIR" && mkdir -p "$APP_DIR/Contents/"{MacOS,Frameworks,Resources}
cp "$PUBLISH_DIR/Ryujinx" "$APP_DIR/Contents/MacOS/" && chmod +x "$APP_DIR/Contents/MacOS/Ryujinx"
cp "$PUBLISH_DIR"/*.dylib "$APP_DIR/Contents/Frameworks/" 2>/dev/null || true
cp distribution/macos/Info.plist "$APP_DIR/Contents/"
cp distribution/macos/Ryujinx.icns distribution/macos/updater.sh distribution/legal/THIRDPARTY.md "$APP_DIR/Contents/Resources/"
echo -n "APPL????" > "$APP_DIR/Contents/PkgInfo"

# 4. 修复依赖路径 (关键!)
python3 distribution/macos/bundle_fix_up.py "$APP_DIR" MacOS/Ryujinx

# 5. 签名与权限 (支持 JIT)
# 注意：Entitlements 对性能至关重要
codesign --entitlements distribution/macos/entitlements.xml --force --deep -s - "$APP_DIR"

# 6. 生成交付文档
echo "# $FEATURE_NAME Release Notes" > "$OUTPUT_DIR/RELEASE_NOTES.md"
# (可选) 复制基准测试结果
# cp -r BenchmarkDotNet.Artifacts/results/* "$OUTPUT_DIR/Benchmarks/"
```

## 📝 常用命令速查
- **构建**: `dotnet build -c Release`
- **运行无头模式**: `./Ryujinx.Headless --root-data-dir ./portable --frames 100`
