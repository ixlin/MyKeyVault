#!/usr/bin/env node
// Run on the host as root. Only the isolated validation copy may accept a test upload.
import assert from 'node:assert/strict';
import { execFileSync } from 'node:child_process';
import { randomUUID } from 'node:crypto';
import { createRequire } from 'node:module';
const require = createRequire('/opt/deepseek-harness-0.2.0-rc.2-prebuilt/node_modules/ws/package.json');
const WebSocket = require('ws');
const port = Number(process.argv[2] ?? 3080);
const unit = process.argv[3] ?? 'deepseek-harness.service';
assert.ok([3080, 3082].includes(port));
assert.ok(['deepseek-harness.service', 'sfrost-harness-validation.service'].includes(unit));
const logs = execFileSync('journalctl', ['-u', unit, '-n', '100', '-o', 'cat', '--no-pager'], { encoding: 'utf8' });
const token = [...logs.matchAll(/dsh web: http:\/\/127\.0\.0\.1:\d+\/\?token=([A-Za-z0-9_-]+)/g)].at(-1)?.[1];
assert.ok(token, 'Launch token available');
const base = `http://127.0.0.1:${port}`;
const login = await fetch(`${base}/?token=${token}`, { redirect: 'manual' });
assert.equal(login.status, 303);
const cookie = login.headers.get('set-cookie')?.split(';', 1)[0];
assert.ok(cookie);
async function rpc(method, args) {
  const rpcId = randomUUID();
  const response = await fetch(`${base}/api/${method}`, {
    method: 'POST', headers: { cookie, 'content-type': 'application/json' },
    body: JSON.stringify({ type: 'client-request', rpcId, method, payload: { args } }),
    signal: AbortSignal.timeout(30_000),
  });
  assert.equal(response.status, 200, method);
  const body = await response.json();
  assert.equal(body.result?.ok, true, `${method}: ${body.result?.error?.message ?? 'failure'}`);
  return body.result.value;
}
function snapshot(sessionId) {
  return new Promise((resolve, reject) => {
    const ws = new WebSocket(`ws://127.0.0.1:${port}/api/remote.mux`, { headers: { cookie } });
    const timer = setTimeout(() => { ws.terminate(); reject(new Error('Session snapshot timeout')); }, 30_000);
    ws.on('open', () => ws.send(JSON.stringify({ type: 'open', streamId: randomUUID(), endpoint: 'session/follow', payload: { args: { request: { address: { kind: 'session', sessionId }, maxMessages: 2 } } } })));
    ws.on('error', reject);
    ws.on('message', raw => {
      const message = JSON.parse(raw);
      if (message.type === 'error') { clearTimeout(timer); ws.close(); reject(new Error(message.error?.message ?? 'Stream error')); }
      if (message.type === 'item' && message.value?.type === 'snapshot') { clearTimeout(timer); ws.close(); resolve(message.value); }
    });
  });
}
const sessions = (await rpc('session/list', { _request: {} })).items;
assert.ok(Array.isArray(sessions));
assert.equal(sessions.filter(x => x.running).length, 0, 'No active model turns before maintenance');
console.log(`PASS session list: ${sessions.length}, active turns: 0`);
if (process.argv.includes('--active-only')) process.exit(0);
for (const row of sessions.filter(x => !x.parentSessionId)) {
  const inspected = await snapshot(row.sessionId);
  assert.ok(Array.isArray(inspected.records), 'Existing history records readable');
}
console.log('PASS existing session logs readable');
const credentials = await rpc('credentials/describe', { refs: ['DEEPSEEK_API_KEY'] });
assert.equal(credentials.DEEPSEEK_API_KEY?.configured, true);
console.log('PASS existing model credential retained');
const models = await rpc('session/modelCatalog', {});
const catalog = JSON.stringify(models);
assert.ok(catalog.includes('deepseek-flash'));
assert.ok(catalog.includes('deepseek-v4-pro'));
console.log('PASS Flash and Pro model catalog');
if (port === 3082 && process.argv.includes('--test-upload')) {
  const row = sessions.find(x => !x.parentSessionId);
  assert.ok(row);
  const response = await fetch(`${base}/api/session/uploadFileBinary?sessionId=${encodeURIComponent(row.sessionId)}&name=upgrade-check.txt`, {
    method: 'POST', headers: { cookie, 'content-type': 'application/octet-stream' }, body: '升级隔离测试，不写入生产数据。', signal: AbortSignal.timeout(30_000),
  });
  assert.equal(response.status, 200);
  const result = await response.json();
  assert.equal(result.ok, true, result.error?.code);
  console.log('PASS test attachment upload in isolated state copy');
}
if (port === 3082 && (process.argv.includes('--test-model') || process.argv.includes('--verify-model-result'))) {
  const existing = sessions.find(x => x.cwd === '/var/lib/sfrost-harness-validation-20261003/test-workspace');
  const { sessionId } = process.argv.includes('--verify-model-result')
    ? (assert.ok(existing, 'Isolated model test exists'), existing)
    : await rpc('session/create', { request: { cwd: '/var/lib/sfrost-harness-validation-20261003/test-workspace' } });
  if (!process.argv.includes('--verify-model-result')) {
  await rpc('session/selectModel', { request: { sessionId, provider: 'deepseek-official', model: 'deepseek-flash', reasoningEffort: 'off' } });
  await rpc('session/prompt', { request: { sessionId, requestId: randomUUID(), mode: 'queue', content: [{ type: 'text', text: '升级连接测试：只回复 OK，不调用任何工具。' }] } });
  }
  let finished = false;
  for (let i = 0; i < 30; i++) {
    await new Promise(resolve => setTimeout(resolve, 2000));
    const inspected = await snapshot(sessionId);
    const events = inspected.records.map(x => x.event);
    if (events.some(x => x.type === 'turn/end')) {
      assert.ok(events.some(x => x.type === 'assistant/message' && JSON.stringify(x.data).includes('OK')), 'Flash returned expected test answer');
      console.log('PASS actual Flash Messages API call in isolated new session');
      finished = true;
      break;
    }
  }
  assert.ok(finished, 'Model test completed within 60 seconds');
}
