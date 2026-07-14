const api = require('../../utils/api');

Page({
  data: {
    identifier: '',
    password: '',
    loading: false,
    loginMode: 'wechat'
  },
  onLoad() {
  },
  onInputId(e) { this.setData({ identifier: e.detail.value }); },
  onInputPwd(e) { this.setData({ password: e.detail.value }); },
  switchToWechatLogin() { this.setData({ loginMode: 'wechat' }); },
  switchToAccountBinding() { this.setData({ loginMode: 'binding' }); },
  getWechatCode() {
    return new Promise((resolve, reject) => wx.login({
      success: (res) => res.code ? resolve(res.code) : reject({ message: '微信授权失败，请重试' }),
      fail: () => reject({ message: '微信授权失败，请重试' })
    }));
  },
  async onWechatLogin() {
    if (this.data.loading) return;
    this.setData({ loading: true });
    try {
      const code = await this.getWechatCode();
      const result = await api.wechatLogin(code);
      wx.showToast({ title: result.isNewUser ? '微信账号已创建' : '登录成功', icon: 'success' });
      setTimeout(() => wx.reLaunch({ url: '/pages/dashboard/index' }), 600);
    } catch (err) {
      wx.showToast({ title: err.message || '微信登录失败', icon: 'none' });
    } finally { this.setData({ loading: false }); }
  },
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
      if (this.data.loginMode === 'binding') {
        const code = await this.getWechatCode();
        await api.bindExistingWechat(code, identifier, password);
      } else {
        await api.login(identifier, password);
      }
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
      
      // 根据错误类型提供不同的提示时长和图标
      if (err?.code === -1) {
        // 网络错误，提示时间长一些
        duration = 3000;
        title = '网络连接失败';
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
