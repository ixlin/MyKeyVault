# 新 Vault 安全与 MCP 模型

## 安全原则

1. 私密值不进入应用日志、错误页、分析事件、浏览器 URL 或 MCP 工具响应。
2. 用户之间绝对隔离；任何记录查询均以当前用户 ID 为强制条件。
3. 默认最小权限、最短有效期、可撤销、可审计。
4. 首版采用成熟、可维护的应用层加密，不自行设计密码学协议。

## 加密模型（首版）

- 每条私密记录生成随机 256-bit 数据密钥（DEK）。
- 每个敏感字段用 AES-256-GCM 加密，并保存算法版本、随机 nonce 与认证标签。
- DEK 不直接入库；使用部署环境中独立配置的主密钥（KEK）包裹后保存。
- 密钥材料只从环境变量或受保护的 Secret Store 注入；不能出现在仓库、迁移文件、样例配置、日志和客户端代码。
- 未来可升级为每用户 Vault Key、用户主密码派生密钥、HSM/KMS 包裹和密钥版本轮换；但首版不声称零知识端到端加密。

## 数据对象

| 对象 | 目的 |
| --- | --- |
| `VaultItem` | 用户拥有的记录与非敏感索引元数据 |
| `VaultSecret` | 加密字段密文、加密元数据、密钥版本 |
| `VaultTag` / `VaultItemTag` | 用户私有分类 |
| `McpAccessToken` | 哈希后的 MCP 令牌、范围、有效期、撤销状态 |
| `ControlledUseRequest` | 连接器发起的受控使用请求，不含秘密值 |
| `SecurityAuditEvent` | 不含明文的不可变安全审计事件 |
| `SecretRotation` | 用户确认旧凭据已被轮换/失效的记录 |

## MCP 最小闭环

```text
AI 工具 -> MCP: 列出用户授权范围内的条目元数据
AI 工具 -> MCP: 请求使用某条记录（用途、目标、有效期）
MyKeyVault -> MCP: 返回 requestId 和待用户确认状态（不返回秘密）
用户 -> Web: 确认、拒绝，或在目标平台完成轮换
MyKeyVault: 记录决定和审计；后续连接器才能在安全通道执行注入
```

当前阶段的“调用后动作”实现为：每个受控请求生成轮换待办，用户必须标记为“已轮换/已失效”。这是风险管理与审计闭环，不伪造自动轮换能力。

## 首版连接器接口

首版先提供给 MCP/Skill 适配器调用的受限 HTTP 边界，适配器不获得任何秘密值：

- `POST /api/controlled-use-requests`：使用用户创建的 Bearer token 发起受控请求；请求体只包含 `vaultItemId`、`requestedAction` 与 `reason`，返回 `requestId`、状态和失效时间。
- `GET /api/controlled-use-requests/{requestId}`：查询同一 token 所属用户的请求状态。
- Web 端负责确认、拒绝与“已轮换/已失效”确认；每个决定会写入安全审计。

令牌只展示一次；数据库只保存 SHA-256 哈希、前缀、有效性和撤销状态。接口永远不返回 `VaultSecret` 或其密文。

## MCP 令牌范围

- `vault.items.read_metadata`
- `vault.use_requests.create`
- `vault.use_requests.read`
- `vault.rotation.confirm`

禁止的范围：读取任意敏感字段、批量导出、修改 Vault 条目、执行 SSH/数据库/链上交易。
