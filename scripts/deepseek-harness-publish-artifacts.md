---
name: publish-artifacts
description: 将 DeepSeek Harness 生成的 HTML、Word、PDF、Markdown、文本、JSON、CSV、Excel 或 PowerPoint 产物发布到 sfrost.cn 私有知识库。创建这类文件或用户要求外网链接时必须使用。
---

# 发布产物到 SFROST 知识库

当本轮生成或更新了可交付文件时，完成文件检查后运行：

```bash
sfrost-publish "/绝对路径/文件名" --title "对用户有意义的标题"
```

支持 HTML、HTM、Markdown、TXT、JSON、CSV、DOC、DOCX、PDF、XLSX 和 PPTX，单个文件不超过 50 MB。

发布成功后，把命令返回的 `https://sfrost.cn/kb/d/<uuid>` 链接提供给用户。该链接要求登录 sfrost.cn。不要再提供 `127.0.0.1`、`localhost` 或工作区文件路径作为用户访问链接。

HTML 会在知识库的受限沙箱中预览；Word 等格式提供登录后的下载。发布过程只写入受限队列，不需要也不得尝试使用 sudo、修改 Nginx 或写入 `/opt`。
