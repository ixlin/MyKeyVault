const api = require('../../../utils/api');

Page({
  data: { id: null, item: null, loading: true, passwordVisible: false },
  onLoad(query){ this.setData({ id: Number(query.id) }); this.load(); },
  onShow(){ 
    // 页面显示时重新加载数据，确保从编辑页面返回后数据是最新的
    if (this.data.id) this.load(); 
  },
  async load(){
    this.setData({ loading: true });
    try {
      const data = await api.getAccount(this.data.id);
      // 兼容后端返回的 password/encryptedPassword 字段名差异
      const item = {
        ...data,
        encryptedPassword: data.encryptedPassword ?? data.password ?? ''
      };
      this.setData({ item });
    } catch (e) {
      if (e.code === 401) return wx.reLaunch({ url: '/pages/login/login' });
      if (e.code === 451) { try { await api.acceptTerms(); } catch(_){}; return this.load(); }
      wx.showToast({ title: '加载失败', icon: 'none' });
    } finally { this.setData({ loading: false }); }
  },
  toEdit(){ wx.navigateTo({ url: `/pages/account/edit/edit?id=${this.data.id}` }); },
  togglePassword(){ this.setData({ passwordVisible: !this.data.passwordVisible }); },
  onDelete(){
    wx.showModal({ title: '删除确认', content: '删除后不可恢复', success: async (r)=>{
      if (!r.confirm) return;
      try{ await api.deleteAccount(this.data.id); wx.showToast({ title: '已删除'}); setTimeout(()=>wx.navigateBack(), 300); }
      catch(e){ wx.showToast({ title: '删除失败', icon: 'none' }); }
    }});
  }
});
