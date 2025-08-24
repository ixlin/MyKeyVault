Component({
  data: {
    selected: 0,
    list: [
      { pagePath: "/pages/account/index/index", text: "首页", icon:"🏠", activeIcon:"🏡" },
      { pagePath: "/pages/account/add/add", text: "新增", icon:"➕", activeIcon:"✳️" },
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
