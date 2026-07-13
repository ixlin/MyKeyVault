# MyKeyVault 微信小程序无界面 CI

本目录使用微信官方 `miniprogram-ci`，不依赖微信开发者工具 GUI，Mac 锁屏时也可运行。

## 凭据

默认从以下本机路径读取上传密钥：

```text
~/.openclaw/credentials/wechat-miniprogram/private.wx8097f65bff21cd68.key
```

密钥不得复制进仓库、日志或消息。也可通过 `WECHAT_MINIPROGRAM_PRIVATE_KEY_PATH` 指定其他本机路径。

## 命令

```bash
cd tools/miniprogram-ci
npm ci --ignore-scripts
npm run verify
```

依赖版本由 `package-lock.json` 锁定。不要运行 `npm audit fix --force`；微信官方工具包含较老的间接编译依赖，强制升级可能破坏小程序编译兼容性。

以下命令会把代码发送给微信，必须在 owner 当轮明确授权后运行：

```bash
npm run preview -- --desc "登录功能真机预览"
npm run upload -- --version 1.2.3 --desc "微信登录体验版"
```

预览二维码默认保存为 `/tmp/mykeyvault-miniprogram-preview.png`。提交审核和正式发布不属于本脚本范围，仍需单独授权。
