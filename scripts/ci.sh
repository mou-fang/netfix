#!/usr/bin/env bash
# NetMedic 本地 CI 脚本
# 用法: bash scripts/ci.sh
# 在 Windows Git Bash 或 Linux 上运行。WPF 构建仅 Windows 可用。

set -euo pipefail

SLN="NetMedic.slnx"

echo "=== [1/4] dotnet format (verify) ==="
dotnet format "$SLN" --verify-no-changes --verbosity minimal

echo "=== [2/4] dotnet build ==="
dotnet build "$SLN" --configuration Debug --nologo

echo "=== [3/4] dotnet test ==="
dotnet test "$SLN" --configuration Debug --no-build --nologo --verbosity normal

echo "=== [4/4] 完成 ==="
echo "所有本地 CI 检查通过。"
