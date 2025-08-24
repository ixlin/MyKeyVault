// 简易 API 封装（适配 Cookie 会话 + 451 条款）
const BASE = 'http://localhost:5158'; // 体验版部署时替换为公网 HTTPS 域名

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
          reject({ code, message: '未登录' }); 
        }
        else if (code === 451){ 
          console.warn(`📋 [API] 451 Terms Required: ${method} ${url}`);
          reject({ code, message: '需接受条款' }); 
        }
        else { 
          console.error(`❌ [API] Error ${code}: ${method} ${url}`, res.data);
          reject({ code, message: res.data?.message || '请求失败' }); 
        }
      },
      fail(err){ 
        console.error(`💥 [API] Network Error: ${method} ${url}`, err);
        reject({ code: -1, message: err.errMsg || '网络错误' }); 
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
