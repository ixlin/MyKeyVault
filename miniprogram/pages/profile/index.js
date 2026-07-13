const api = require('../../utils/api');
const { BASE } = require('../../utils/config');

function avatarUrl(value) {
  if (!value || /^https?:\/\//.test(value)) return value || '';
  return BASE + value;
}

Page({
  data: {
    loading: true,
    saving: false,
    email: '',
    savedEmail: '',
    nickname: '',
    avatarUrl: '',
    wechatOpenId: '',
    isEmailConfirmed: false
  },

  onShow() {
    if (this.getTabBar && this.getTabBar()) {
      this.getTabBar().setData({ selected: 3 });
    }
    this.loadProfile();
  },

  async loadProfile() {
    this.setData({ loading: true });
    try {
      const me = await api.me();
      if (!me.isAuthenticated) {
        wx.reLaunch({ url: '/pages/login/login' });
        return;
      }
      const email = me.email || '';
      this.setData({
        email,
        savedEmail: email,
        nickname: me.wechatNickname || '',
        avatarUrl: avatarUrl(me.wechatAvatarUrl),
        wechatOpenId: me.wechatOpenId || '',
        isEmailConfirmed: !!me.isEmailConfirmed,
        loading: false
      });
    } catch (err) {
      this.setData({ loading: false });
      wx.showToast({ title: err.message || '个人信息加载失败', icon: 'none' });
    }
  },

  onEmailInput(e) {
    this.setData({ email: e.detail.value });
  },

  onNicknameInput(e) {
    this.setData({ nickname: e.detail.value });
  },

  async onChooseAvatar(e) {
    const filePath = e.detail && e.detail.avatarUrl;
    if (!filePath || this.data.saving) return;
    this.setData({ saving: true });
    try {
      const result = await api.uploadProfileAvatar(filePath);
      this.setData({ avatarUrl: avatarUrl(result.wechatAvatarUrl) });
      wx.showToast({ title: '头像已保存', icon: 'success' });
    } catch (err) {
      wx.showToast({ title: err.message || '头像保存失败', icon: 'none' });
    } finally {
      this.setData({ saving: false });
    }
  },

  async saveNickname() {
    if (this.data.saving) return;
    const nickname = this.data.nickname.trim();
    if (!nickname) {
      wx.showToast({ title: '请填写昵称', icon: 'none' });
      return;
    }
    this.setData({ saving: true });
    try {
      const result = await api.updateProfile(nickname);
      this.setData({ nickname: result.wechatNickname || nickname });
      wx.showToast({ title: '昵称已保存', icon: 'success' });
    } catch (err) {
      wx.showToast({ title: err.message || '昵称保存失败', icon: 'none' });
    } finally {
      this.setData({ saving: false });
    }
  },

  async saveEmail() {
    if (this.data.saving) return;
    const email = this.data.email.trim();
    if (!email) {
      wx.showToast({ title: '请输入邮箱地址', icon: 'none' });
      return;
    }
    if (!/^\S+@\S+\.\S+$/.test(email)) {
      wx.showToast({ title: '请输入有效邮箱', icon: 'none' });
      return;
    }
    if (email === this.data.savedEmail && !this.data.isEmailConfirmed) {
      wx.showToast({ title: '验证邮件已发送，请查收', icon: 'none' });
      return;
    }

    this.setData({ saving: true });
    try {
      const result = await api.bindEmail(email);
      this.setData({
        email: result.email || email,
        savedEmail: result.email || email,
        isEmailConfirmed: false
      });
      wx.showToast({ title: '验证邮件已发送', icon: 'success' });
    } catch (err) {
      wx.showToast({ title: err.message || '邮箱保存失败', icon: 'none' });
    } finally {
      this.setData({ saving: false });
    }
  },

  onPullDownRefresh() {
    this.loadProfile().finally(() => wx.stopPullDownRefresh());
  }
});
