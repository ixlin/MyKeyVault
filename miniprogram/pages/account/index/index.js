const api = require('../../../utils/api');

Page({
  data: {
    loading: true,
    items: [],
    q: ''
  },
  onLoad() { 
    console.log('📱 [INDEX] onLoad triggered');
    // onLoad 在页面首次加载时触发，我们将逻辑统一放到 onShow 中处理
  },
  onShow() {
    console.log('📱 [INDEX] onShow triggered');
    // onShow 会在页面每次显示时触发，包括从登录页跳转回来
    // 我们在这里统一处理登录检查和数据加载
    this.ensureLoginThenLoad();
  },
  async ensureLoginThenLoad() {
    console.log('🔍 [INDEX] ensureLoginThenLoad starting...');
    this.setData({ loading: true }); // 开始时显示加载中
    try {
      // 1. 检查登录状态
      console.log('🔍 [INDEX] Checking login status...');
      const me = await api.me();
      console.log('🔍 [INDEX] Login status result:', me);
      
      if (!me.isAuthenticated) {
        // 如果未登录，直接跳转到登录页
        console.log('🚪 [INDEX] Not authenticated, redirecting to login');
        wx.reLaunch({ url: '/pages/login/login' });
        return; // 终止后续操作
      }

      console.log('✅ [INDEX] User is authenticated, loading data...');
      // 2. 如果已登录，加载列表
      await this.loadList();

    } catch (e) {
      console.error('❌ [INDEX] Error in ensureLoginThenLoad:', e);
      
      if (e.code === 401) {
        // 捕获到 401 错误，意味着 session 过期或无效，跳转登录
        console.log('🚪 [INDEX] 401 error, redirecting to login');
        wx.reLaunch({ url: '/pages/login/login' });
      } else if (e.code === 451) {
        // 捕获到 451 错误，意味着需要接受条款
        console.log('📋 [INDEX] 451 error, accepting terms...');
        try {
          // 尝试自动接受条款
          await api.acceptTerms();
          console.log('✅ [INDEX] Terms accepted, retrying data load...');
          // 接受后，重新加载数据
          await this.loadList();
        } catch (_) {
          // 如果接受条款时出错（例如网络问题），提示用户
          console.error('❌ [INDEX] Failed to accept terms:', _);
          wx.showToast({ title: '条款更新失败，请重试', icon: 'none' });
          this.setData({ loading: false });
        }
      } else {
        // 其他未知网络错误
        console.error('❌ [INDEX] Unknown error:', e);
        wx.showToast({ title: '网络错误，请稍后重试', icon: 'none' });
        this.setData({ loading: false });
      }
    }
  },
  async loadList() {
    console.log('📋 [INDEX] loadList starting...');
    // 仅负责加载数据和更新界面，不再处理复杂的错误逻辑
    this.setData({ loading: true });
    try {
      const data = await api.listAccounts(this.data.q);
      console.log('📋 [INDEX] loadList success:', data);
      this.setData({ items: data.items || [], loading: false });
    } catch (e) {
      console.error('❌ [INDEX] loadList failed:', e);
      // 在 ensureLoginThenLoad 中已经处理了 401 和 451
      // 如果在这里仍然遇到，说明有其他问题，例如在已登录状态下被吊销权限
      // 为避免无限循环，这里只做提示
      wx.showToast({ title: '加载列表失败', icon: 'none' });
      this.setData({ loading: false });
    }
  },
  onSearchInput(e) { this.setData({ q: e.detail.value }); },
  onSearch(){ this.loadList(); },
  toDetail(e){ const id = e.currentTarget.dataset.id; wx.navigateTo({ url: `/pages/account/detail/detail?id=${id}`}); },
  toCreate(){ wx.navigateTo({ url: '/pages/account/edit/edit' }); }
});
