'use strict'

const fs = require('node:fs')
const os = require('node:os')
const path = require('node:path')

const APP_ID = 'wx8097f65bff21cd68'
const PROJECT_PATH = path.resolve(__dirname, '../../miniprogram')
const PROJECT_CONFIG_PATH = path.join(PROJECT_PATH, 'project.config.json')
const DEFAULT_KEY_PATH = path.join(
  os.homedir(),
  '.openclaw',
  'credentials',
  'wechat-miniprogram',
  `private.${APP_ID}.key`,
)
const PRIVATE_KEY_PATH = process.env.WECHAT_MINIPROGRAM_PRIVATE_KEY_PATH || DEFAULT_KEY_PATH

const args = process.argv.slice(2)
const command = args[0] || 'verify'

function option(name, fallback = undefined) {
  const index = args.indexOf(`--${name}`)
  if (index === -1 || index + 1 >= args.length) return fallback
  return args[index + 1]
}

function assert(condition, message) {
  if (!condition) throw new Error(message)
}

function validateLocalConfiguration(requireKey) {
  assert(fs.existsSync(PROJECT_CONFIG_PATH), `Missing project config: ${PROJECT_CONFIG_PATH}`)

  const projectConfig = JSON.parse(fs.readFileSync(PROJECT_CONFIG_PATH, 'utf8'))
  assert(projectConfig.appid === APP_ID, `AppID mismatch: expected ${APP_ID}`)
  assert(projectConfig.compileType === 'miniprogram', 'project.config.json is not a miniProgram project')

  const appConfigPath = path.join(PROJECT_PATH, 'app.json')
  assert(fs.existsSync(appConfigPath), `Missing app config: ${appConfigPath}`)
  JSON.parse(fs.readFileSync(appConfigPath, 'utf8'))

  if (requireKey) {
    assert(fs.existsSync(PRIVATE_KEY_PATH), 'WeChat upload key is not installed in the OpenClaw credential store')
    const mode = fs.statSync(PRIVATE_KEY_PATH).mode & 0o777
    assert((mode & 0o077) === 0, 'WeChat upload key permissions are too broad; expected mode 600')
  }

  return projectConfig
}

function createProject() {
  const ci = require('miniprogram-ci')
  return {
    ci,
    project: new ci.Project({
      appid: APP_ID,
      type: 'miniProgram',
      projectPath: PROJECT_PATH,
      privateKeyPath: PRIVATE_KEY_PATH,
      ignores: ['node_modules/**/*', 'project.private.config.json'],
    }),
  }
}

function withTimeout(promise, timeoutMs, label) {
  let timer
  const timeout = new Promise((_, reject) => {
    timer = setTimeout(() => reject(new Error(`${label} timed out after ${timeoutMs / 1000}s`)), timeoutMs)
  })

  return Promise.race([promise, timeout]).finally(() => clearTimeout(timer))
}

function progress(update) {
  if (!update) return
  if (typeof update === 'string') {
    console.log(update.slice(0, 240))
    return
  }

  const message = update.message || update.status || update._msg || update.phase
  const percent = update.progress ?? update.percent
  if (message || percent !== undefined) {
    console.log(`${message || 'progress'}${percent !== undefined ? ` (${percent}%)` : ''}`.slice(0, 240))
  }
}

async function verify() {
  validateLocalConfiguration(true)
  const { project } = createProject()
  const files = project.getFileList()
  assert(Array.isArray(files) && files.length > 0, 'No files were discovered for the mini program package')
  console.log(`verify=ok appid=${APP_ID} files=${files.length}`)
}

async function preview() {
  validateLocalConfiguration(true)
  const { ci, project } = createProject()
  const output = option('output', '/tmp/mykeyvault-miniprogram-preview.png')
  const desc = option('desc', 'MyKeyVault headless preview')

  await withTimeout(
    ci.preview({
      project,
      desc,
      setting: { useProjectConfig: true },
      qrcodeFormat: 'image',
      qrcodeOutputDest: output,
      onProgressUpdate: progress,
    }),
    300_000,
    'WeChat mini program preview',
  )

  console.log(`preview=ok qrcode=${output}`)
}

async function upload() {
  validateLocalConfiguration(true)
  const version = option('version')
  assert(version && /^[0-9]+(?:\.[0-9]+){1,3}(?:[-+][0-9A-Za-z.-]+)?$/.test(version), 'Upload requires --version, for example 1.2.3')

  const desc = option('desc', `MyKeyVault ${version}`)
  const robot = Number(option('robot', '1'))
  assert(Number.isInteger(robot) && robot >= 1 && robot <= 30, '--robot must be an integer from 1 to 30')

  const { ci, project } = createProject()
  await withTimeout(
    ci.upload({
      project,
      version,
      desc,
      robot,
      setting: { useProjectConfig: true },
      onProgressUpdate: progress,
    }),
    300_000,
    'WeChat mini program upload',
  )

  console.log(`upload=ok version=${version} robot=${robot}`)
}

async function main() {
  if (command === 'verify') return verify()
  if (command === 'preview') return preview()
  if (command === 'upload') return upload()
  throw new Error(`Unknown command: ${command}`)
}

main().catch((error) => {
  console.error(`miniprogram-ci error: ${error.message}`)
  process.exitCode = 1
})
