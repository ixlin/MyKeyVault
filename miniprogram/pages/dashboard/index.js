const api = require('../../utils/api');

Page({
  data: {
    loading: true,
    accountCount: 0,
    tagCount: 0,
    recentAccounts: []
  },

  onLoad() {
    console.log('📊 [DASHBOARD] onLoad triggered');
  },

  onShow() {
    console.log('📊 [DASHBOARD] onShow triggered');
    // 同步自定义 TabBar 选中态
    if (this.getTabBar && this.getTabBar()) {
      this.getTabBar().setData({ selected: 0 });
    }
    
    // 如果数据还没有加载过，则显示加载状态；否则直接刷新数据
    if (this.data.accountCount === 0 && this.data.recentAccounts.length === 0) {
      // 首次加载，显示loading
      this.setData({ loading: true });
      this.ensureLoginThenLoad();
    } else {
      // 已有数据，静默刷新
      this.setData({ loading: false });
      // 延迟刷新数据，避免阻塞页面显示
      setTimeout(() => {
        this.silentRefresh();
      }, 100);
    }
  },

  async silentRefresh() {
    console.log('🔄 [DASHBOARD] Silent refresh starting...');
    try {
      // 检查登录状态
      const me = await api.me();
      if (!me.isAuthenticated) {
        console.log('🚪 [DASHBOARD] Not authenticated, redirecting to login');
        wx.reLaunch({ url: '/pages/login/login' });
        return;
      }

      console.log('✅ [DASHBOARD] User is authenticated, refreshing dashboard data...');
      await this.loadDashboardData();
    } catch (e) {
      console.error('❌ [DASHBOARD] Error in silentRefresh:', e);
      // 静默刷新失败时不显示错误，保持现有数据
    }
  },

  async ensureLoginThenLoad() {
    console.log('🔍 [DASHBOARD] ensureLoginThenLoad starting...');
    // 注意：loading状态已在onShow中设置，这里不再重复设置
    
    try {
      // 检查登录状态
      const me = await api.me();
      if (!me.isAuthenticated) {
        console.log('🚪 [DASHBOARD] Not authenticated, redirecting to login');
        wx.reLaunch({ url: '/pages/login/login' });
        return;
      }

      console.log('✅ [DASHBOARD] User is authenticated, loading dashboard data...');
      await this.loadDashboardData();

    } catch (e) {
      console.error('❌ [DASHBOARD] Error in ensureLoginThenLoad:', e);
      
      if (e.code === 401) {
        wx.reLaunch({ url: '/pages/login/login' });
      } else if (e.code === 451) {
        try {
          await api.acceptTerms();
          await this.loadDashboardData();
        } catch (_) {
          wx.showToast({ title: '条款更新失败，请重试', icon: 'none' });
          // 即使条款接受失败，也显示基本页面
          this.setData({ loading: false });
        }
      } else {
        wx.showToast({ title: '网络错误，请稍后重试', icon: 'none' });
        // 即使网络错误，也显示基本页面
        this.setData({ loading: false });
      }
    }
  },

  async loadDashboardData() {
    try {
      console.log('📊 [DASHBOARD] Calling getDashboardStats API...');
      // 调用仪表盘统计接口
      const dashboardData = await api.getDashboardStats();
      console.log('📊 [DASHBOARD] getDashboardStats response:', dashboardData);
      
      // 格式化最近账号的时间
      const recentAccounts = dashboardData.recentAccounts.map(account => ({
        ...account,
        formattedTime: this.formatTime(account.lastModified)
      }));

      this.setData({
        accountCount: dashboardData.accountCount,
        tagCount: dashboardData.tagCount,
        recentAccounts,
        loading: false
      });

      console.log('📊 [DASHBOARD] Dashboard data loaded:', {
        accountCount: dashboardData.accountCount,
        tagCount: dashboardData.tagCount,
        recentAccountsCount: recentAccounts.length
      });

    } catch (e) {
      console.error('❌ [DASHBOARD] Failed to load dashboard data:', e);
      
      // 如果仪表盘 API 失败，尝试使用原有的方式获取数据
      try {
        console.log('🔄 [DASHBOARD] Falling back to original data loading...');
        const [accountsResult, tagsResult] = await Promise.all([
          api.listAccounts('', null),
          api.listTags(false)
        ]);

        console.log('📊 [DASHBOARD] Fallback data loaded:', {
          accountsResult,
          tagsResult
        });

        const accounts = accountsResult.items || [];
        const tags = tagsResult.items || [];

        // 获取最近修改的5条账号
        const recentAccounts = accounts
          .sort((a, b) => new Date(b.updatedAt || b.UpdatedAt) - new Date(a.updatedAt || a.UpdatedAt))
          .slice(0, 5)
          .map(account => ({
            ...account,
            formattedTime: this.formatTime(account.updatedAt || account.UpdatedAt)
          }));

        this.setData({
          accountCount: accounts.length,
          tagCount: tags.length,
          recentAccounts,
          loading: false
        });
      } catch (fallbackError) {
        console.error('❌ [DASHBOARD] Fallback also failed:', fallbackError);
        wx.showToast({ title: '加载数据失败', icon: 'none' });
        this.setData({ loading: false });
      }
    }
  },

  formatTime(dateStr) {
    const date = new Date(dateStr);
    const now = new Date();
    const diff = now - date;
    
    const minutes = Math.floor(diff / (1000 * 60));
    const hours = Math.floor(diff / (1000 * 60 * 60));
    const days = Math.floor(diff / (1000 * 60 * 60 * 24));
    
    if (minutes < 60) {
      return `${minutes}分钟前`;
    } else if (hours < 24) {
      return `${hours}小时前`;
    } else if (days < 7) {
      return `${days}天前`;
    } else {
      return date.toLocaleDateString('zh-CN');
    }
  },

  // 点击账号总数卡片，跳转到账号页面
  onAccountsCardTap() {
    wx.switchTab({
      url: '/pages/accounts/index'
    });
  },

  // 点击标签总数卡片，跳转到标签页面  
  onTagsCardTap() {
    wx.switchTab({
      url: '/pages/tags/index/index'
    });
  },

  // 点击最近账号项，跳转到账号详情
  onRecentAccountTap(e) {
    const { id } = e.currentTarget.dataset;
    wx.navigateTo({
      url: `/pages/account/detail/detail?id=${id}`
    });
  },

  // 下拉刷新
  onPullDownRefresh() {
    this.loadDashboardData().finally(() => {
      wx.stopPullDownRefresh();
    });
  },

  // 点击新增账号按钮
  onAddAccountTap() {
    wx.navigateTo({
      url: '/pages/account/add/add'
    });
  }
});
