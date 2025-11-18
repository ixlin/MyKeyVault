#!/bin/bash

# 打包发布文件为 ZIP 格式
# 方便分发给用户

echo "开始打包发布文件..."
echo ""

cd publish

# 打包 Windows 版本
echo "正在打包 Windows x64 版本..."
zip -r MyKeyVault.Finance.CLI-win-x64.zip win-x64/ README.txt -q
echo "✓ Windows x64 打包完成: MyKeyVault.Finance.CLI-win-x64.zip"

# 打包 macOS Intel 版本
echo "正在打包 macOS Intel 版本..."
zip -r MyKeyVault.Finance.CLI-osx-x64.zip osx-x64/ README.txt -q
echo "✓ macOS Intel 打包完成: MyKeyVault.Finance.CLI-osx-x64.zip"

# 打包 macOS ARM64 版本
echo "正在打包 macOS ARM64 版本..."
zip -r MyKeyVault.Finance.CLI-osx-arm64.zip osx-arm64/ README.txt -q
echo "✓ macOS ARM64 打包完成: MyKeyVault.Finance.CLI-osx-arm64.zip"

echo ""
echo "=========================================="
echo "打包完成！"
echo "=========================================="
echo ""
echo "生成的 ZIP 文件："
ls -lh *.zip | awk '{print "  " $9 " (" $5 ")"}'
echo ""
echo "分发说明："
echo "  Windows 用户: MyKeyVault.Finance.CLI-win-x64.zip"
echo "  macOS Intel: MyKeyVault.Finance.CLI-osx-x64.zip"
echo "  macOS Apple Silicon (M1/M2/M3): MyKeyVault.Finance.CLI-osx-arm64.zip"
echo ""
