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
    loading: false
  },
  onLoad(query){
    if (query.id) { this.setData({ id: Number(query.id) }); this.load(); wx.setNavigationBarTitle({ title: '编辑账号' }); }
    else { wx.setNavigationBarTitle({ title: '新建账号' }); }
  },
  async load(){
    try{
      const d = await api.getAccount(this.data.id);
      this.setData({ 
        title: d.title || '', 
        username: d.username || '', 
        encryptedPassword: d.encryptedPassword || '', // 显示现有密码
        originalPassword: d.encryptedPassword || '', // 保存原始密码
        website: d.website || '', 
        note: d.note || '' 
      });
    }catch(e){ 
      if (e.code===401) return wx.reLaunch({ url:'/pages/login/login'}); 
      if (e.code===451){ try{ await api.acceptTerms(); }catch(_){}; return this.load(); } 
      wx.showToast({ title:'加载失败', icon:'none'}); 
    }
  },
  onInput(e){ const { k } = e.currentTarget.dataset; this.setData({ [k]: e.detail.value }); },
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
          tagIds: []
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
          tagIds: []
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
