@echo off
REM MyKeyVault Finance CLI 发布脚本 (Windows)
REM 生成 Windows 和 macOS 自包含可执行程序

echo ==========================================
echo MyKeyVault Finance CLI 发布工具
echo ==========================================
echo.

SET PROJECT_PATH=MyKeyVault.Finance.CLI.csproj
SET OUTPUT_BASE=.\publish

REM 清理旧的发布文件
echo 清理旧的发布文件...
if exist "%OUTPUT_BASE%" rd /s /q "%OUTPUT_BASE%"

REM 发布 Windows x64 版本
echo.
echo 正在发布 Windows x64 版本...
dotnet publish "%PROJECT_PATH%" ^
  -c Release ^
  -r win-x64 ^
  --self-contained true ^
  -p:PublishSingleFile=true ^
  -p:PublishTrimmed=false ^
  -p:IncludeNativeLibrariesForSelfExtract=true ^
  -o "%OUTPUT_BASE%\win-x64"

if %ERRORLEVEL% EQU 0 (
    echo [OK] Windows x64 版本发布成功
) else (
    echo [FAIL] Windows x64 版本发布失败
    exit /b 1
)

REM 发布 macOS x64 版本
echo.
echo 正在发布 macOS x64 (Intel) 版本...
dotnet publish "%PROJECT_PATH%" ^
  -c Release ^
  -r osx-x64 ^
  --self-contained true ^
  -p:PublishSingleFile=true ^
  -p:PublishTrimmed=false ^
  -p:IncludeNativeLibrariesForSelfExtract=true ^
  -o "%OUTPUT_BASE%\osx-x64"

if %ERRORLEVEL% EQU 0 (
    echo [OK] macOS x64 版本发布成功
) else (
    echo [FAIL] macOS x64 版本发布失败
    exit /b 1
)

REM 发布 macOS ARM64 版本
echo.
echo 正在发布 macOS ARM64 (Apple Silicon) 版本...
dotnet publish "%PROJECT_PATH%" ^
  -c Release ^
  -r osx-arm64 ^
  --self-contained true ^
  -p:PublishSingleFile=true ^
  -p:PublishTrimmed=false ^
  -p:IncludeNativeLibrariesForSelfExtract=true ^
  -o "%OUTPUT_BASE%\osx-arm64"

if %ERRORLEVEL% EQU 0 (
    echo [OK] macOS ARM64 版本发布成功
) else (
    echo [FAIL] macOS ARM64 版本发布失败
    exit /b 1
)

REM 创建 README 文件
echo.
echo 生成使用说明文件...
(
echo MyKeyVault Finance CLI - 财务报表导出工具
echo ==========================================
echo.
echo 使用说明：
echo.
echo Windows 用户：
echo 1. 打开 win-x64 文件夹
echo 2. 双击运行 MyKeyVault.Finance.CLI.exe
echo 3. 按照提示输入股票代码、年度和报告期
echo.
echo macOS Intel 用户：
echo 1. 打开 osx-x64 文件夹
echo 2. 在终端中运行：./MyKeyVault.Finance.CLI
echo    或者双击可执行文件（首次需要在"系统偏好设置-^>安全性"中允许）
echo 3. 按照提示输入股票代码、年度和报告期
echo.
echo macOS Apple Silicon 用户：
echo 1. 打开 osx-arm64 文件夹
echo 2. 在终端中运行：./MyKeyVault.Finance.CLI
echo    或者双击可执行文件（首次需要在"系统偏好设置-^>安全性"中允许）
echo 3. 按照提示输入股票代码、年度和报告期
echo.
echo 配置文件：
echo - appsettings.json 包含 API 配置信息
echo - 如需修改 API 地址或密钥，请编辑此文件
echo.
echo 导出文件：
echo - 默认导出到当前目录
echo - 文件名格式：股票代码_年度_周期.xlsx
echo.
echo 注意事项：
echo - 本程序已包含所有运行时依赖，无需额外安装 .NET
echo - 首次在 macOS 上运行可能需要在安全设置中允许
echo - Windows 可能会提示 SmartScreen，点击"仍要运行"即可
echo.
echo 支持与反馈：
echo 如有问题，请联系技术支持。
echo.
echo 版本信息：v1.0.0
echo 发布日期：2025-11-18
) > "%OUTPUT_BASE%\README.txt"

echo.
echo ==========================================
echo 发布完成！
echo ==========================================
echo.
echo 发布文件位置：
echo   Windows x64:         %OUTPUT_BASE%\win-x64\
echo   macOS Intel:         %OUTPUT_BASE%\osx-x64\
echo   macOS Apple Silicon: %OUTPUT_BASE%\osx-arm64\
echo.
echo 可执行文件：
echo   Windows: MyKeyVault.Finance.CLI.exe
echo   macOS:   MyKeyVault.Finance.CLI
echo.
echo 现在可以将对应平台的文件夹打包发送给用户。
echo ==========================================
pause
