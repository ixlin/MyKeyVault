Page({
  onShow(){ if(this.getTabBar && this.getTabBar()){ this.getTabBar().setData({ selected: 1 }); } },
  toCreate(){ wx.navigateTo({ url: '/pages/account/edit/edit' }); }
});
