#!/usr/bin/env bash
# 安装 git pre-commit 钩子，提交前自动检查格式和构建。
# 用法: bash scripts/install-hooks.sh

set -euo pipefail

HOOK_DIR="$(git rev-parse --git-dir)/hooks"
mkdir -p "$HOOK_DIR"

cat > "$HOOK_DIR/pre-commit" <<'EOF'
#!/usr/bin/env bash
set -euo pipefail
echo "[pre-commit] dotnet format --verify-no-changes"
dotnet format NetMedic.slnx --verify-no-changes --verbosity minimal || {
  echo "[pre-commit] 格式检查失败，请运行: dotnet format NetMedic.slnx"
  exit 1
}
echo "[pre-commit] 格式检查通过。"
EOF

chmod +x "$HOOK_DIR/pre-commit"
echo "已安装 pre-commit 钩子到 $HOOK_DIR/pre-commit"
