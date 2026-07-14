const api = require('../../../utils/api')

Page({
  data: { tags: [], newName: '', isGuest: true },
  onShow(){ if(this.getTabBar && this.getTabBar()){ this.getTabBar().setData({ selected: 2 }); } this.load() },
  async load(){
    try{
      const me = await api.me();
      if (!me.isAuthenticated) return this.setData({ tags: [], isGuest: true });
      this.setData({ isGuest: false });
      const res = await api.listTags(true);
      this.setData({ tags: res.items || [] });
    }catch(err){
      if(err?.code===401){ this.setData({ tags: [], isGuest: true }); }
      else if(err?.code===451){ wx.showModal({ title:'需先接受条款', content:'请前往网页端接受服务条款后重试' }) }
      else{ wx.showToast({ icon:'none', title: err.message||'加载失败' }) }
    }
  },
  onInputNew(e){ this.setData({ newName: e.detail.value }) },
  async onAdd(){
    const name = (this.data.newName||'').trim();
    if(!name){ wx.showToast({ icon:'none', title:'请输入名称' }); return; }
    if(this.data.isGuest){
      wx.showModal({ title:'登录后保存标签', content:'标签会在登录后保存到你的密码本。现在去登录？', confirmText:'去登录', success:({confirm})=>{ if(confirm) wx.navigateTo({url:'/pages/login/login'}); } });
      return;
    }
    try{
      await api.createTag(name);
      this.setData({ newName:'' });
      this.load();
    }catch(e){
      if(e.code===409){ wx.showToast({ icon:'none', title:'名称已存在' }); }
      else{ wx.showToast({ icon:'none', title:e.message||'添加失败' }); }
    }
  },
  onLongPress(e){
    const { id, name } = e.currentTarget.dataset;
    wx.showActionSheet({
      itemList:['重命名','删除'],
      success: async (res)=>{
        if(res.tapIndex===0){
          wx.showModal({
            title:'重命名',
            editable:true,
            placeholderText:'新名称',
            content: name,
            success: async ({ confirm, content })=>{
              if(!confirm) return;
              try{ await api.renameTag(id, content); this.load(); }
              catch(e){ if(e.code===409){ wx.showToast({ icon:'none', title:'名称已存在' }); } else { wx.showToast({ icon:'none', title:e.message||'失败' }); } }
            }
          })
        }else if(res.tapIndex===1){
          try{
            await api.deleteTag(id);
            this.load();
          }catch(e){
            if(e.code===409){
              const count = e.count || 0;
              wx.showModal({
                title:'标签仍在使用',
                content:`共有 ${count} 条账号仍使用该标签，是否强制删除？`,
                success: async ({ confirm })=>{ if(confirm){ try{ await api.deleteTag(id, true); this.load(); } catch(err){ wx.showToast({ icon:'none', title: err.message||'删除失败' }); } } }
              })
            }else{
              wx.showToast({ icon:'none', title:e.message||'删除失败' })
            }
          }
        }
      }
    })
  }
})
