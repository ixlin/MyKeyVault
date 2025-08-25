const api = require('../../utils/api');

Page({
  data: {
    loading: true,
    items: [],
    q: '',
    tagId: null,
    tags: [],
    searchHistory: []
  },

  onLoad() { 
    console.log('📱 [ACCOUNTS] onLoad triggered');
  },

  onShow() {
    console.log('📱 [ACCOUNTS] onShow triggered');
    // 同步自定义 TabBar 选中态
    if (this.getTabBar && this.getTabBar()) { 
      this.getTabBar().setData({ selected: 1 }); 
    }
    this.ensureLoginThenLoad();
  },

  async ensureLoginThenLoad() {
    console.log('🔍 [ACCOUNTS] ensureLoginThenLoad starting...');
    this.setData({ loading: true });
    
    try {
      // 检查登录状态
      console.log('🔍 [ACCOUNTS] Checking login status...');
      const me = await api.me();
      console.log('🔍 [ACCOUNTS] Login status result:', me);
      
      if (!me.isAuthenticated) {
        console.log('🚪 [ACCOUNTS] Not authenticated, redirecting to login');
        wx.reLaunch({ url: '/pages/login/login' });
        return;
      }

      console.log('✅ [ACCOUNTS] User is authenticated, loading data...');
      // 加载标签与列表，以及搜索历史
      await Promise.all([
        this.loadTags(), 
        this.loadList(),
        this.loadSearchHistory()
      ]);

    } catch (e) {
      console.error('❌ [ACCOUNTS] Error in ensureLoginThenLoad:', e);
      
      if (e.code === 401) {
        console.log('🚪 [ACCOUNTS] 401 error, redirecting to login');
        wx.reLaunch({ url: '/pages/login/login' });
      } else if (e.code === 451) {
        console.log('📋 [ACCOUNTS] 451 error, accepting terms...');
        try {
          await api.acceptTerms();
          console.log('✅ [ACCOUNTS] Terms accepted, retrying data load...');
          await this.loadList();
        } catch (_) {
          console.error('❌ [ACCOUNTS] Failed to accept terms:', _);
          wx.showToast({ title: '条款更新失败，请重试', icon: 'none' });
          this.setData({ loading: false });
        }
      } else {
        console.error('❌ [ACCOUNTS] Unknown error:', e);
        wx.showToast({ title: '网络错误，请稍后重试', icon: 'none' });
        this.setData({ loading: false });
      }
    }
  },

  async loadList() {
    console.log('📋 [ACCOUNTS] loadList starting...');
    this.setData({ loading: true });
    
    try {
      const data = await api.listAccounts(this.data.q, this.data.tagId);
      console.log('📋 [ACCOUNTS] loadList success:', data);
      const items = (data.items||[]).map(it=>({
        ...it,
        formattedTime: formatShortTime(it.updatedAt)
      }));
      this.setData({ items, loading: false });
    } catch (e) {
      console.error('❌ [ACCOUNTS] loadList failed:', e);
      wx.showToast({ title: '加载列表失败', icon: 'none' });
      this.setData({ loading: false });
    }
  },

  async loadTags(){
    try{
      const r = await api.listTags(false);
      this.setData({ tags: r.items || [] });
    }catch(e){ 
      console.error('❌ [ACCOUNTS] loadTags failed:', e);
    }
  },

  // 加载搜索历史
  loadSearchHistory() {
    try {
      const history = wx.getStorageSync('searchHistory') || [];
      this.setData({ searchHistory: history.slice(0, 5) }); // 只保留最近5条
    } catch (e) {
      console.error('❌ [ACCOUNTS] loadSearchHistory failed:', e);
    }
  },

  // 保存搜索历史
  saveSearchHistory(keyword) {
    if (!keyword || keyword.trim() === '') return;
    
    try {
      let history = wx.getStorageSync('searchHistory') || [];
      // 移除重复项
      history = history.filter(item => item !== keyword);
      // 添加到开头
      history.unshift(keyword);
      // 限制最多10条
      history = history.slice(0, 10);
      
      wx.setStorageSync('searchHistory', history);
      this.setData({ searchHistory: history.slice(0, 5) });
    } catch (e) {
      console.error('❌ [ACCOUNTS] saveSearchHistory failed:', e);
    }
  },

  onSearchInput(e) { 
    this.setData({ q: e.detail.value }); 
  },

  onSearch() { 
    const keyword = this.data.q.trim();
    if (keyword) {
      this.saveSearchHistory(keyword);
    }
    this.loadList(); 
  },

  // 点击搜索历史
  onSearchHistoryTap(e) {
    const keyword = e.currentTarget.dataset.keyword;
    this.setData({ q: keyword }, () => {
      this.loadList();
    });
  },

  // 清除搜索历史
  onClearSearchHistory() {
    wx.removeStorageSync('searchHistory');
    this.setData({ searchHistory: [] });
    wx.showToast({ title: '搜索历史已清除', icon: 'success' });
  },

  onTagPick(e){
    const idStr = e.currentTarget.dataset.id;
    const id = idStr === '' || idStr === undefined ? null : Number(idStr);
    this.setData({ tagId: id }, () => this.loadList());
  },

  toDetail(e){ 
    const id = e.currentTarget.dataset.id; 
    wx.navigateTo({ url: `/pages/account/detail/detail?id=${id}`}); 
  },

  // 新增按钮点击，直接跳转到创建页面
  toCreate(){ 
    wx.navigateTo({ url: '/pages/account/add/add' }); 
  },

  // 下拉刷新
  onPullDownRefresh() {
    Promise.all([this.loadTags(), this.loadList()]).finally(() => {
      wx.stopPullDownRefresh();
    });
  }
});

// 简短时间格式：今天显示 HH:mm，其它显示 MM-DD
function formatShortTime(iso){
  try{
    const d = new Date(iso);
    const now = new Date();
    const sameDay = d.getFullYear()===now.getFullYear() && d.getMonth()===now.getMonth() && d.getDate()===now.getDate();
    const pad = (n)=> (n<10? '0'+n : ''+n);
    if(sameDay){ return `${pad(d.getHours())}:${pad(d.getMinutes())}`; }
    return `${pad(d.getMonth()+1)}-${pad(d.getDate())}`;
  }catch(_){ return ''; }
}
