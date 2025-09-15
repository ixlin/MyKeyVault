const api = require('../../../utils/api');

Page({
  data: {
    title: '',
    username: '',
    password: '',
    url: '',
    notes: '',
    loading: false,
    availableTags: [],
    selectedTagIds: [],
    passwordVisible: false
  },

  onLoad() {
    this.loadTags();
  },

  // 加载可用标签
  async loadTags() {
    try {
      console.log('🏷️ [ADD] Loading available tags...');
      const result = await api.listTags();
      console.log('🏷️ [ADD] Tags loaded:', result);
      
      if (result && result.items) {
        // 确保标签ID为数字类型，并添加选中状态标识
        const tagsWithSelection = result.items.map(tag => ({
          ...tag,
          id: parseInt(tag.id), // 确保ID为数字
          isSelected: false // 初始化选中状态
        }));
        
        this.setData({
          availableTags: tagsWithSelection
        });
        console.log('🏷️ [ADD] Set availableTags:', tagsWithSelection);
      } else {
        console.log('🏷️ [ADD] No tags found in response');
        this.setData({
          availableTags: []
        });
      }
    } catch (error) {
      console.error('🏷️ [ADD] 加载标签失败:', error);
      wx.showToast({
        title: '加载标签失败',
        icon: 'none'
      });
    }
  },

  // 切换标签选择状态
  toggleTag(e) {
    const tagId = parseInt(e.currentTarget.dataset.id); // 确保转换为数字
    const { selectedTagIds, availableTags } = this.data;
    
    console.log('🏷️ [ADD] Toggle tag:', tagId, 'current selected:', selectedTagIds);
    console.log('🏷️ [ADD] TagId type:', typeof tagId, 'Selected type:', typeof selectedTagIds[0]);
    
    const index = selectedTagIds.indexOf(tagId);
    let newSelectedIds;
    
    if (index === -1) {
      // 添加标签
      newSelectedIds = [...selectedTagIds, tagId];
      console.log('🏷️ [ADD] Adding tag:', tagId);
    } else {
      // 移除标签
      newSelectedIds = selectedTagIds.filter(id => id !== tagId);
      console.log('🏷️ [ADD] Removing tag:', tagId);
    }

    // 更新标签的选中状态
    const updatedTags = availableTags.map(tag => ({
      ...tag,
      isSelected: newSelectedIds.includes(tag.id)
    }));
    
    this.setData({
      selectedTagIds: newSelectedIds,
      availableTags: updatedTags
    });
    
    console.log('🏷️ [ADD] New selected tags:', newSelectedIds);
  },

  onShow() {
    // 不设置 TabBar 选中态，因为这是一个独立页面
  },

  // 输入变化处理
  onInputChange(e) {
    const { field } = e.currentTarget.dataset;
    const { value } = e.detail;
    this.setData({
      [field]: value
    });
  },

  // 取消操作
  onCancel() {
    wx.navigateBack();
  },

  togglePassword(){
    this.setData({ passwordVisible: !this.data.passwordVisible });
  },

  // 表单提交
  async onSubmit() {
    const { title, username, password, url, notes, selectedTagIds } = this.data;
    
    // 验证必填字段
    if (!title.trim()) {
      wx.showToast({
        title: '请输入账号标题',
        icon: 'none'
      });
      return;
    }

    this.setData({ loading: true });

    try {
      // 调用创建账号 API
      await api.createAccount({
        Title: title.trim(),
        Username: username.trim() || null,
        EncryptedPassword: password || null,
        Website: url.trim() || null,
        Note: notes.trim() || null,
        TagIds: selectedTagIds.length > 0 ? selectedTagIds : null
      });

      wx.showToast({
        title: '创建成功',
        icon: 'success'
      });

      // 返回上一页并刷新
      setTimeout(() => {
        wx.navigateBack();
      }, 1500);

    } catch (error) {
      console.error('创建账号失败:', error);
      wx.showToast({
        title: error.message || '创建失败',
        icon: 'none'
      });
    } finally {
      this.setData({ loading: false });
    }
  }
});
