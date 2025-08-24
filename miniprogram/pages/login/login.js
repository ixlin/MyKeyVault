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
      // 跳转到首页，由首页进行后续检查
      wx.reLaunch({ url: '/pages/account/index/index' });
    } catch (err) {
      console.error('❌ [LOGIN] Login failed:', err);
      wx.showToast({ title: err?.message || '登录失败', icon: 'none' });
    } finally {
      this.setData({ loading: false });
    }
  }
});
