// 环境自适应的后端 BASE 地址
// - 开发(dev): http://localhost:5000
// - 体验/正式(trial/release): https://mykeyvault.cn
// 支持通过本地存储覆盖：wx.setStorageSync('BASE_URL_OVERRIDE', 'https://your.domain')

function detectEnv() {
  try {
    const info = wx.getAccountInfoSync && wx.getAccountInfoSync();
    return (info && info.miniProgram && info.miniProgram.envVersion) || 'develop';
  } catch (_) {
    return 'develop';
  }
}

function getBaseUrl() {
  try {
    const override = wx.getStorageSync && wx.getStorageSync('BASE_URL_OVERRIDE');
    if (override && typeof override === 'string') return override;
  } catch (_) {}
  const env = detectEnv();
  // 真机调试时使用线上服务器，避免 localhost 无法访问
  // 如需本地开发，在开发者工具的「详情」→「本地设置」→勾选「不校验合法域名」
  // if (env === 'develop') return 'http://localhost:5158';
  
  // 统一使用线上服务器（支持真机调试）
  return 'https://mykeyvault.cn';
}

const BASE = getBaseUrl();

module.exports = { BASE };
