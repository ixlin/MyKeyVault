Component({
  data: {
    selected: 0,
    list: [
      { pagePath: "/pages/dashboard/index", text: "首页", icon:"🏠", activeIcon:"🏡" },
      { pagePath: "/pages/accounts/index", text: "账号", icon:"📱", activeIcon:"📲" },
      { pagePath: "/pages/tags/index/index", text: "标签", icon:"🏷️", activeIcon:"🔖" }
    ]
  },
  methods: {
    switchTab(e){
      const index = e.currentTarget.dataset.index;
      const url = this.data.list[index].pagePath;
      wx.switchTab({ url });
      this.setData({ selected: index });
    }
  }
});
