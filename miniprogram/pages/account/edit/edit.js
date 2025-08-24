const api = require('../../../utils/api');

Page({
  data: {
    id: null,
    title: '',
    username: '',
    encryptedPassword: '',
    originalPassword: '', // 保存原始密码，用于判断是否修改
    website: '',
    note: '',
    tagIds: [], // 仅 ID
    selectedTags: [], // {id,name}
    allTags: [], // 供名称映射
    tagPopup: false,
    loading: false
  },
  onLoad(query){
    if (query.id) { this.setData({ id: Number(query.id) }); this.load(); wx.setNavigationBarTitle({ title: '编辑账号' }); }
    else { wx.setNavigationBarTitle({ title: '新建账号' }); }
  },
  async load(){
    try{
      const d = await api.getAccount(this.data.id);
      // 同时加载全部标签用于名称映射
      let allTags = [];
      try { const r = await api.listTags(false); allTags = r.items || []; } catch(_){}
      const tagIds = (d.tags||[]).map(t=>t.id);
      const selectedTags = tagIds.map(id=> allTags.find(t=>t.id===id)).filter(Boolean);
      this.setData({ 
        title: d.title || '', 
        username: d.username || '', 
        encryptedPassword: d.encryptedPassword || '', // 显示现有密码
        originalPassword: d.encryptedPassword || '', // 保存原始密码
        website: d.website || '', 
        note: d.note || '',
        tagIds,
        selectedTags,
        allTags
      });
    }catch(e){ 
      if (e.code===401) return wx.reLaunch({ url:'/pages/login/login'}); 
      if (e.code===451){ try{ await api.acceptTerms(); }catch(_){}; return this.load(); } 
      wx.showToast({ title:'加载失败', icon:'none'}); 
    }
  },
  onInput(e){ const { k } = e.currentTarget.dataset; this.setData({ [k]: e.detail.value }); },
  openTags(){ this.ensureAllTagsThen(() => this.setData({ tagPopup: true })); },
  closeTags(){ this.setData({ tagPopup: false }); },
  onTagsChange(e){
    const ids = e.detail.value || [];
    const selectedTags = ids.map(id=> (this.data.allTags||[]).find(t=>t.id===id)).filter(Boolean);
    this.setData({ tagIds: ids, selectedTags });
  },
  async ensureAllTagsThen(cb){
    if ((this.data.allTags||[]).length>0) return cb && cb();
    try{ const r = await api.listTags(false); this.setData({ allTags: r.items||[] }); }catch(_){ wx.showToast({ title:'加载标签失败', icon:'none' }); }
    cb && cb();
  },
  async onSave(){
    if (this.data.loading) return; 
    this.setData({ loading: true });
    try{
      if (this.data.id){
        // 编辑模式：只有当密码实际被修改时才发送密码字段
        const passwordChanged = this.data.encryptedPassword !== this.data.originalPassword;
        await api.updateAccount(this.data.id, {
          title: this.data.title,
          username: this.data.username,
          encryptedPassword: passwordChanged ? this.data.encryptedPassword : '', // 只有修改了才发送
          website: this.data.website,
          note: this.data.note,
          tagIds: this.data.tagIds
        });
        wx.showToast({ title: '已保存' });
        wx.navigateBack();
      } else {
        // 新建模式：直接发送所有字段
        const r = await api.createAccount({
          title: this.data.title,
          username: this.data.username,
          encryptedPassword: this.data.encryptedPassword,
          website: this.data.website,
          note: this.data.note,
          tagIds: this.data.tagIds
        });
        wx.showToast({ title: '已创建' });
        wx.navigateBack();
      }
    }catch(e){ 
      wx.showToast({ title: e?.message || '保存失败', icon:'none' }); 
    }
    finally{ 
      this.setData({ loading:false }); 
    }
  }
});
