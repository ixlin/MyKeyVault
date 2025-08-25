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
    value(v){ 
      const selected = Array.isArray(v) ? v.slice() : [];
      this.setData({ selected });
      this.updateTagsWithSelection();
    },
    'selected'() {
      this.updateTagsWithSelection();
    }
  },
  methods: {
    async init(){
      try{
        const res = await api.listTags(false);
        const tags = (res.items || []).map(tag => ({
          ...tag,
          id: parseInt(tag.id), // 确保ID为数字类型
          isSelected: false
        }));
        this.setData({ 
          tags,
          selected: (this.data.value||[]).slice() 
        });
        this.updateTagsWithSelection();
      }catch(e){
        wx.showToast({ title: '加载标签失败', icon: 'none' });
      }
    },
    updateTagsWithSelection() {
      const { tags, selected } = this.data;
      const selectedSet = new Set(selected.map(id => parseInt(id)));
      const updatedTags = tags.map(tag => ({
        ...tag,
        isSelected: selectedSet.has(parseInt(tag.id))
      }));
      this.setData({ tags: updatedTags });
    },
    isSelected(id){ return (this.data.selected||[]).includes(id); },
    toggle(e){
      const id = parseInt(e.currentTarget.dataset.id);
      const selected = new Set((this.data.selected||[]).map(i => parseInt(i)));
      if (selected.has(id)) selected.delete(id); else selected.add(id);
      const newSelected = Array.from(selected);
      this.setData({ selected: newSelected });
    },
    hide(){ this.triggerEvent('close'); },
    onBgTap(e){ this.hide(); },
    confirm(){ this.triggerEvent('change', { value: this.data.selected.slice() }); this.hide(); },
    noop(){}
  }
});
