const api = require('../../utils/api');

Page({
  data: {
    identifier: '',
    password: '',
    loading: false,
  },
  onLoad() {},
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
      
      // 根据错误类型提供不同的提示时长和图标
      if (err?.code === -1) {
        // 网络错误，提示时间长一些
        duration = 3000;
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
  }
});
