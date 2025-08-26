// 简易 API 封装（适配 Cookie 会话 + 451 条款）
const { BASE } = require('./config');

// 存储会话信息
let sessionCookie = '';

function request(method, url, data){
  return new Promise((resolve, reject)=>{
    console.log(`🚀 [API] ${method} ${url}`, data ? { data } : '');
    console.log(`🍪 [API] Current cookie: ${sessionCookie || 'None'}`);
    
    const requestOptions = {
      method,
      url: BASE + url,
      data,
      header: { 'Content-Type': 'application/json' },
      withCredentials: true,
      success(res){
        const code = res.statusCode;
        console.log(`✅ [API] ${method} ${url} -> ${code}`, res.data);
        
        // 检查是否有新的 Cookie（主要是登录后）
        if (res.header && (res.header['Set-Cookie'] || res.header['set-cookie'])) {
          const setCookie = res.header['Set-Cookie'] || res.header['set-cookie'];
          console.log(`🍪 [API] Received Set-Cookie:`, setCookie);
          if (Array.isArray(setCookie)) {
            sessionCookie = setCookie.find(c => c.includes('.AspNetCore.Identity.Application')) || '';
          } else if (typeof setCookie === 'string') {
            sessionCookie = setCookie.includes('.AspNetCore.Identity.Application') ? setCookie : '';
          }
          if (sessionCookie) {
            console.log(`🍪 [API] Stored session cookie: ${sessionCookie.substring(0, 50)}...`);
          }
        }
        
        if (code >= 200 && code < 300){ 
          resolve(res.data||{}); 
        }
        else if (code === 401){ 
          console.warn(`🔐 [API] 401 Unauthorized: ${method} ${url}`);
          // 清除无效的 cookie
          sessionCookie = '';
          const errorData = res.data || {};
          const errorCode = errorData.code || '';
          let message = '登录失败';
          
          // 根据错误代码提供具体提示
          switch(errorCode) {
            case 'USER_NOT_FOUND':
              message = '账号不存在';
              break;
            case 'WRONG_PASSWORD':
              message = '密码错误';
              break;
            case 'ACCOUNT_LOCKED':
              message = '账号已被锁定，请稍后再试';
              break;
            case 'ACCOUNT_NOT_ALLOWED':
              message = '账号未激活或被禁用';
              break;
            case 'TWO_FACTOR_REQUIRED':
              message = '需要双重验证';
              break;
            default:
              message = errorData.message || '登录失败';
          }
          
          reject({ code, message }); 
        }
        else if (code === 400) {
          console.warn(`📝 [API] 400 Bad Request: ${method} ${url}`);
          const errorData = res.data || {};
          const message = errorData.message || '请求参数错误';
          reject({ code, message });
        }
        else if (code === 403) {
          console.warn(`🚫 [API] 403 Forbidden: ${method} ${url}`);
          reject({ code, message: '权限不足' });
        }
        else if (code === 451){ 
          console.warn(`📋 [API] 451 Terms Required: ${method} ${url}`);
          reject({ code, message: '需接受条款' }); 
        }
        else if (code === 500) {
          console.error(`💥 [API] 500 Server Error: ${method} ${url}`);
          reject({ code, message: '服务器内部错误，请稍后再试' });
        }
        else { 
          console.error(`❌ [API] Error ${code}: ${method} ${url}`, res.data);
          const errorData = res.data || {};
          reject({ code, message: errorData.message || `请求失败 (${code})` }); 
        }
      },
      fail(err){ 
        console.error(`💥 [API] Network Error: ${method} ${url}`, err);
        let message = '网络连接失败';
        
        // 根据不同的网络错误提供更具体的提示
        if (err.errMsg) {
          if (err.errMsg.includes('timeout')) {
            message = '请求超时，请检查网络连接';
          } else if (err.errMsg.includes('fail')) {
            message = '网络连接失败，请检查网络设置';
          } else if (err.errMsg.includes('abort')) {
            message = '请求被中断';
          } else {
            message = err.errMsg;
          }
        }
        
        reject({ code: -1, message }); 
      }
    };

    // 如果有存储的 cookie，手动添加到请求头中
    if (sessionCookie && url !== '/api/mp/auth/login') {
      requestOptions.header.Cookie = sessionCookie.split(';')[0]; // 只取主要部分
      console.log(`🍪 [API] Adding cookie to request: ${requestOptions.header.Cookie}`);
    }

    wx.request(requestOptions);
  });
}

module.exports = {
  me(){ return request('GET', '/api/mp/auth/me'); },
  login(identifier, password){ return request('POST', '/api/mp/auth/login', { identifier, password }); },
  logout(){ return request('POST', '/api/mp/auth/logout'); },
  acceptTerms(){ return request('POST', '/api/mp/legal/accept'); },
  // dashboard
  getDashboardStats(){ return request('GET', '/api/mp/dashboard/stats'); },
  listAccounts(q, tagId){ 
    const params = [];
    if (q) params.push('q=' + encodeURIComponent(q));
    if (tagId) params.push('tagId=' + encodeURIComponent(tagId));
    const qs = params.length ? ('?' + params.join('&')) : '';
    return request('GET', '/api/mp/vault/accounts' + qs); 
  },
  getAccount(id){ return request('GET', `/api/mp/vault/accounts/${id}`); },
  createAccount(body){ return request('POST', '/api/mp/vault/accounts', body); },
  updateAccount(id, body){ return request('PUT', `/api/mp/vault/accounts/${id}`, body); },
  deleteAccount(id){ return request('DELETE', `/api/mp/vault/accounts/${id}`); },
  // tags
  listTags(withCounts=false){ return request('GET', '/api/mp/tags' + (withCounts? '?counts=true' : '')); },
  createTag(name){ return request('POST', '/api/mp/tags', { name }); },
  renameTag(id, name){ return request('PUT', `/api/mp/tags/${id}`, { name }); },
  deleteTag(id, force=false){ return request('DELETE', `/api/mp/tags/${id}` + (force? '?force=true' : '')); }
};
