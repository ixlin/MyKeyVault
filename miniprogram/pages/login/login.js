const api = require('../../utils/api');

Page({
  data: {
    identifier: '',
    password: '',
    loading: false,
    showDebug: false,
    debugInfo: {
      env: '',
      baseUrl: '',
      networkType: '检测中...',
      connectionStatus: '检测中...',
      lastError: ''
    }
  },
  onLoad() {
    console.log('🔐 [LOGIN] Page loaded');
    
    // 初始化调试信息
    this.initDebugInfo();
    
    // 检查网络状态
    wx.getNetworkType({
      success: (res) => {
        console.log('🌐 [LOGIN] Network type:', res.networkType);
        this.updateDebugInfo('networkType', res.networkType);
        if (res.networkType === 'none') {
          this.updateDebugInfo('lastError', '网络连接失败，请检查网络设置');
          wx.showToast({ 
            title: '网络连接失败，请检查网络设置', 
            icon: 'none',
            duration: 3000
          });
        }
      },
      fail: (err) => {
        console.error('❌ [LOGIN] Failed to get network type:', err);
        this.updateDebugInfo('networkType', '获取失败');
        this.updateDebugInfo('lastError', '无法获取网络状态: ' + JSON.stringify(err));
      }
    });
    
    // 测试服务器连通性
    this.testServerConnection();
  },

  // 初始化调试信息
  initDebugInfo() {
    const { BASE } = require('../../utils/config');
    const env = this.detectEnv();
    
    this.setData({
      'debugInfo.env': env,
      'debugInfo.baseUrl': BASE
    });
  },

  // 检测小程序环境
  detectEnv() {
    try {
      const info = wx.getAccountInfoSync && wx.getAccountInfoSync();
      return (info && info.miniProgram && info.miniProgram.envVersion) || 'develop';
    } catch (_) {
      return 'develop';
    }
  },

  // 更新调试信息
  updateDebugInfo(key, value) {
    this.setData({
      [`debugInfo.${key}`]: value
    });
  },

  // 切换调试面板显示
  toggleDebug() {
    this.setData({
      showDebug: !this.data.showDebug
    });
  },
  
  testServerConnection() {
    const { BASE } = require('../../utils/config');
    console.log('🔗 [LOGIN] Testing server connection to:', BASE);
    
    this.updateDebugInfo('connectionStatus', '正在测试连接...');
    
    wx.request({
      url: BASE + '/api/mp/tags',
      method: 'GET',
      timeout: 15000,
      success: (res) => {
        console.log('✅ [LOGIN] Server connection test result:', res.statusCode);
        if (res.statusCode === 401) {
          console.log('✅ [LOGIN] Server is accessible (401 expected for unauthenticated request)');
          this.updateDebugInfo('connectionStatus', '✅ 服务器可访问 (401认证失败，正常)');
          this.updateDebugInfo('lastError', '');
        } else if (res.statusCode === 200) {
          this.updateDebugInfo('connectionStatus', '✅ 服务器连接正常');
          this.updateDebugInfo('lastError', '');
        } else {
          this.updateDebugInfo('connectionStatus', `⚠️ 异常状态码: ${res.statusCode}`);
          this.updateDebugInfo('lastError', `服务器返回状态码: ${res.statusCode}`);
        }
      },
      fail: (err) => {
        console.error('❌ [LOGIN] Server connection test failed:', err);
        let errorMsg = '服务器连接失败';
        let debugError = JSON.stringify(err);
        
        if (err.errMsg) {
          if (err.errMsg.includes('timeout')) {
            errorMsg = '⏰ 连接超时 (15秒)';
            debugError = '请求超时，可能是网络问题或服务器响应慢';
          } else if (err.errMsg.includes('fail')) {
            errorMsg = '❌ 连接失败';
            if (err.errMsg.includes('ssl')) {
              debugError = 'SSL/HTTPS 连接失败，可能是证书问题';
            } else if (err.errMsg.includes('domain')) {
              debugError = '域名解析失败或域名未配置到合法域名列表';
            } else {
              debugError = err.errMsg;
            }
          }
        }
        
        this.updateDebugInfo('connectionStatus', errorMsg);
        this.updateDebugInfo('lastError', debugError);
        
        // 如果是关键错误，自动显示调试面板
        if (err.errMsg && (err.errMsg.includes('domain') || err.errMsg.includes('ssl'))) {
          this.setData({ showDebug: true });
        }
        
        wx.showToast({
          title: '服务器连接失败',
          icon: 'none',
          duration: 3000
        });
      }
    });
  },
  onInputId(e) { this.setData({ identifier: e.detail.value }); },
  onInputPwd(e) { this.setData({ password: e.detail.value }); },
  async onSubmit() {
    console.log('🔐 [LOGIN] Login button clicked');
    if (this.data.loading) {
      console.log('🔐 [LOGIN] Already loading, ignoring click');
      return;
    }
    const { identifier, password } = this.data;
    if (!identifier || !password) {
      console.log('🔐 [LOGIN] Missing credentials');
      wx.showToast({ title: '请输入账号与密码', icon: 'none' });
      return;
    }
    
    console.log('🔐 [LOGIN] Starting login process...');
    this.setData({ loading: true });
    try {
      // 登录页只负责登录，登录成功后直接跳转
      console.log('🔐 [LOGIN] Calling api.login...');
      await api.login(identifier, password);
      console.log('✅ [LOGIN] Login successful, redirecting...');
      
      // 显示成功提示
      wx.showToast({ 
        title: '登录成功', 
        icon: 'success',
        duration: 1000
      });
      
      // 延迟跳转，让用户看到成功提示
      setTimeout(() => {
        wx.reLaunch({ url: '/pages/dashboard/index' });
      }, 1000);
      
    } catch (err) {
      console.error('❌ [LOGIN] Login failed:', err);
      
      let title = '登录失败';
      let duration = 2000;
      
      // 记录到调试信息
      this.updateDebugInfo('lastError', `登录失败: ${err?.message || JSON.stringify(err)}`);
      
      // 根据错误类型提供不同的提示时长和图标
      if (err?.code === -1) {
        // 网络错误，提示时间长一些
        duration = 3000;
        title = '网络连接失败';
        this.updateDebugInfo('connectionStatus', '❌ 网络连接失败');
      } else if (err?.message) {
        title = err.message;
      }
      
      wx.showToast({ 
        title: title, 
        icon: 'none',
        duration: duration
      });
    } finally {
      this.setData({ loading: false });
    }
  },

  // 处理注册账号点击事件
  onRegisterTap() {
    console.log('🔗 [LOGIN] Register link tapped');
    this.openWebsite('注册账号');
  },

  // 处理忘记密码点击事件
  onForgotPasswordTap() {
    console.log('🔗 [LOGIN] Forgot password link tapped');
    this.openWebsite('找回密码');
  },

  // 打开PC端网站
  openWebsite(action) {
    const { BASE } = require('../../utils/config');
    // 从配置中获取域名，但要确保使用HTTPS
    let websiteUrl = BASE;
    if (websiteUrl.includes('localhost')) {
      // 如果是本地开发环境，提示用户
      wx.showModal({
        title: '提示',
        content: '开发环境下请在电脑浏览器访问 http://localhost:5158 进行' + action,
        showCancel: false
      });
      return;
    }

    // 生产环境，打开网站
    wx.showModal({
      title: action,
      content: '即将跳转到PC端网站进行' + action + '，是否继续？',
      confirmText: '打开网站',
      cancelText: '取消',
      success: (res) => {
        if (res.confirm) {
          console.log('🔗 [LOGIN] Opening website:', websiteUrl);
          // 复制链接到剪贴板并提示用户
          wx.setClipboardData({
            data: websiteUrl,
            success: () => {
              wx.showModal({
                title: '链接已复制',
                content: `网站链接已复制到剪贴板：\n${websiteUrl}\n\n请在浏览器中粘贴访问`,
                showCancel: false,
                confirmText: '知道了'
              });
            },
            fail: () => {
              // 如果复制失败，显示链接让用户手动复制
              wx.showModal({
                title: 'PC端网站',
                content: `请在电脑浏览器中访问：\n${websiteUrl}`,
                showCancel: false,
                confirmText: '知道了'
              });
            }
          });
        }
      }
    });
  }
});
