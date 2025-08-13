/* Minimal E2EE helper: PBKDF2 + AES-GCM(v1). No plaintext leaves the browser. */
const E2EE = (() => {
  const textEncoder = new TextEncoder();
  const textDecoder = new TextDecoder();

  const toB64 = (u8) => btoa(String.fromCharCode(...u8));
  const fromB64 = (b64) => new Uint8Array(atob(b64).split('').map(c => c.charCodeAt(0)));

  async function derive(master, saltB64, iterations = 310000) {
    const salt = fromB64(saltB64);
    const keyMaterial = await crypto.subtle.importKey(
      'raw', textEncoder.encode(master), { name: 'PBKDF2' }, false, ['deriveBits', 'deriveKey']
    );
    const params = { name: 'PBKDF2', hash: 'SHA-256', salt, iterations };
    const kek = await crypto.subtle.deriveKey(params, keyMaterial, { name: 'AES-GCM', length: 256 }, true, ['wrapKey', 'unwrapKey', 'encrypt', 'decrypt']);
    const authBits = await crypto.subtle.deriveBits(params, keyMaterial, 256);
    const kAuth = toB64(new Uint8Array(authBits));
    return { kek, kAuth };
  }

  async function generateDEK() {
    return crypto.subtle.generateKey({ name: 'AES-GCM', length: 256 }, true, ['encrypt', 'decrypt']);
  }

  async function wrapDEK(kek, dek) {
    const iv = crypto.getRandomValues(new Uint8Array(12));
    const wrapped = await crypto.subtle.wrapKey('raw', dek, kek, { name: 'AES-GCM', iv });
    return { iv: toB64(iv), ct: toB64(new Uint8Array(wrapped)) };
  }

  async function unwrapDEK(kek, wrapped) {
    const iv = fromB64(wrapped.iv);
    const ct = fromB64(wrapped.ct);
    return crypto.subtle.unwrapKey('raw', ct, kek, { name: 'AES-GCM', iv }, { name: 'AES-GCM', length: 256 }, true, ['encrypt', 'decrypt']);
  }

  async function encrypt(dek, plaintext) {
    const iv = crypto.getRandomValues(new Uint8Array(12));
    const data = new TextEncoder().encode(plaintext);
    const ct = await crypto.subtle.encrypt({ name: 'AES-GCM', iv }, dek, data);
    return { v: 'v1', alg: 'AES-GCM', iv: toB64(iv), ct: toB64(new Uint8Array(ct)) };
  }

  async function decrypt(dek, payload) {
    const iv = fromB64(payload.iv);
    const ct = fromB64(payload.ct);
    const pt = await crypto.subtle.decrypt({ name: 'AES-GCM', iv }, dek, ct);
    return new TextDecoder().decode(pt);
  }

  function isCipherText(text) {
    try {
      const obj = JSON.parse(text);
      return obj && obj.v === 'v1' && obj.alg === 'AES-GCM' && obj.iv && obj.ct;
    } catch { return false; }
  }

  async function exportKeyB64(key) {
    const raw = new Uint8Array(await crypto.subtle.exportKey('raw', key));
    return toB64(raw);
  }
  async function importAesKeyFromB64(b64) {
    const raw = fromB64(b64);
    return crypto.subtle.importKey('raw', raw, { name: 'AES-GCM', length: 256 }, true, ['encrypt', 'decrypt']);
  }

  return { derive, generateDEK, wrapDEK, unwrapDEK, encrypt, decrypt, isCipherText, exportKeyB64, importAesKeyFromB64, toB64, fromB64 };
})();

/* Simple mask helpers */
function maskString(s, visible = 0) {
  if (!s) return '';
  if (visible <= 0) return '••••••••';
  const head = s.slice(0, visible);
  return head + '••••';
}
