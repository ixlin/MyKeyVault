const api = require('../../utils/api');

Component({
  properties: {
    visible: { type: Boolean, value: false },
    // 预设选中ID数组
    value: { type: Array, value: [] }
  },
  data: {
    tags: [],
    selected: []
  },
  observers: {
    visible(v){ if (v) { this.init(); } },
    value(v){ this.setData({ selected: Array.isArray(v) ? v.slice() : [] }); }
  },
  methods: {
    async init(){
      try{
        const res = await api.listTags(false);
        this.setData({ tags: res.items || [], selected: (this.data.value||[]).slice() });
      }catch(e){
        wx.showToast({ title: '加载标签失败', icon: 'none' });
      }
    },
    isSelected(id){ return (this.data.selected||[]).includes(id); },
    toggle(e){
      const id = e.currentTarget.dataset.id;
      const selected = new Set(this.data.selected||[]);
      if (selected.has(id)) selected.delete(id); else selected.add(id);
      this.setData({ selected: Array.from(selected) });
    },
    hide(){ this.triggerEvent('close'); },
    onBgTap(e){ this.hide(); },
    confirm(){ this.triggerEvent('change', { value: this.data.selected.slice() }); this.hide(); },
    noop(){}
  }
});
