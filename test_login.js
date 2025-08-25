const fetch = require('node-fetch');

async function testLogin(identifier, password, description) {
  console.log(`\n=== 测试: ${description} ===`);
  try {
    const response = await fetch('http://localhost:5000/api/mp/auth/login', {
      method: 'POST',
      headers: {
        'Content-Type': 'application/json'
      },
      body: JSON.stringify({ identifier, password })
    });
    
    const data = await response.json();
    console.log(`状态码: ${response.status}`);
    console.log(`响应: ${JSON.stringify(data, null, 2)}`);
  } catch (error) {
    console.log(`网络错误: ${error.message}`);
  }
}

async function runTests() {
  // 测试不存在的用户
  await testLogin('wronguser@qq.com', 'Admin.888888', '不存在的账号');
  
  // 测试错误密码
  await testLogin('sfrost@qq.com', 'wrongpassword', '错误密码');
  
  // 测试正确登录
  await testLogin('sfrost@qq.com', 'Admin.888888', '正确登录');
  
  // 测试空参数
  await testLogin('', '', '空参数');
}

runTests().catch(console.error);
