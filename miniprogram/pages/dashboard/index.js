const api = require('../../utils/api');

Page({
  data: { loading: true, isGuest: true, accountCount: 0, tagCount: 0, recentAccounts: [], emailReminder: false, email: '' },
  onShow() {
    if (this.getTabBar && this.getTabBar()) this.getTabBar().setData({ selected: 0 });
    this.loadHome();
  },
  async loadHome() {
    this.setData({ loading: true });
    try {
      const me = await api.me();
      if (!me.isAuthenticated) return this.setData({ loading: false, isGuest: true, accountCount: 0, tagCount: 0, recentAccounts: [] });
      this.setData({ isGuest: false, emailReminder: !!me.emailReminder, email: me.email || '' });
      const result = await api.getDashboardStats();
      this.setData({ accountCount: result.accountCount || 0, tagCount: result.tagCount || 0, recentAccounts: (result.recentAccounts || []).map(x => ({ ...x, formattedTime: this.formatTime(x.lastModified) })), loading: false });
    } catch (_) {
      this.setData({ loading: false, isGuest: true });
    }
  },
  formatTime(value) { const d = new Date(value); const hours = Math.floor((Date.now() - d) / 3600000); return hours < 1 ? '刚刚更新' : hours < 24 ? `${hours}小时前` : d.toLocaleDateString('zh-CN'); },
  onAccountsCardTap() { wx.switchTab({ url: '/pages/accounts/index' }); },
  onTagsCardTap() { wx.switchTab({ url: '/pages/tags/index/index' }); },
  onRecentAccountTap(e) { wx.navigateTo({ url: `/pages/account/detail/detail?id=${e.currentTarget.dataset.id}` }); },
  onAddAccountTap() { wx.navigateTo({ url: '/pages/account/add/add' }); },
  onLoginTap() { wx.navigateTo({ url: '/pages/login/login' }); },
  onPullDownRefresh() { this.loadHome().finally(() => wx.stopPullDownRefresh()); }
});
