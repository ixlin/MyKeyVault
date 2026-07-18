using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Options;
using MyKeyVault.Vault.Models;

namespace MyKeyVault.Vault.Services;

/// <summary>
/// Encrypts secret fields with a random DEK. The DEK is wrapped by the deployment KEK.
/// Plaintext must never be logged, cached, or returned by an MCP endpoint.
/// </summary>
public sealed class SecretCipher
{
    private const int KeyBytes = 32;
    private const int NonceBytes = 12;
    private const int TagBytes = 16;
    private readonly byte[] _masterKey;

    public SecretCipher(IOptions<VaultEncryptionOptions> options)
    {
        try
        {
            _masterKey = Convert.FromBase64String(options.Value.MasterKey);
        }
        catch (FormatException ex)
        {
            throw new InvalidOperationException("VaultEncryption:MasterKey must be a base64-encoded 32-byte key.", ex);
        }

        if (_masterKey.Length != KeyBytes)
        {
            throw new InvalidOperationException("VaultEncryption:MasterKey must decode to exactly 32 bytes.");
        }
    }

    public VaultSecret Encrypt(Guid vaultItemId, string fieldName, string plaintext)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(fieldName);
        ArgumentNullException.ThrowIfNull(plaintext);

        var dataKey = RandomNumberGenerator.GetBytes(KeyBytes);
        var nonce = RandomNumberGenerator.GetBytes(NonceBytes);
        var plaintextBytes = Encoding.UTF8.GetBytes(plaintext);
        var ciphertext = new byte[plaintextBytes.Length];
        var tag = new byte[TagBytes];
        var wrappedDataKey = new byte[KeyBytes];

        try
        {
            using (var dataCipher = new AesGcm(dataKey, TagBytes))
            {
                dataCipher.Encrypt(nonce, plaintextBytes, ciphertext, tag, Encoding.UTF8.GetBytes(fieldName));
            }

            // A KEK wraps only a random DEK; no secret field is encrypted directly with a static key.
            using (var keyCipher = new AesGcm(_masterKey, TagBytes))
            {
                var keyNonce = RandomNumberGenerator.GetBytes(NonceBytes);
                var wrapTag = new byte[TagBytes];
                keyCipher.Encrypt(keyNonce, dataKey, wrappedDataKey, wrapTag);

                return new VaultSecret
                {
                    VaultItemId = vaultItemId,
                    FieldName = fieldName,
                    Ciphertext = ciphertext,
                    Nonce = nonce,
                    AuthenticationTag = tag,
                    WrappedDataKey = wrappedDataKey,
                    KeyWrapNonce = keyNonce,
                    KeyWrapAuthenticationTag = wrapTag
                };
            }
        }
        finally
        {
            CryptographicOperations.ZeroMemory(dataKey);
            CryptographicOperations.ZeroMemory(plaintextBytes);
        }
    }

    public string Decrypt(VaultSecret secret)
    {
        ArgumentNullException.ThrowIfNull(secret);
        var dataKey = new byte[KeyBytes];
        byte[]? plaintextBytes = null;
        try
        {
            using (var keyCipher = new AesGcm(_masterKey, TagBytes))
                keyCipher.Decrypt(secret.KeyWrapNonce, secret.WrappedDataKey, secret.KeyWrapAuthenticationTag, dataKey);

            plaintextBytes = new byte[secret.Ciphertext.Length];
            using (var dataCipher = new AesGcm(dataKey, TagBytes))
                dataCipher.Decrypt(secret.Nonce, secret.Ciphertext, secret.AuthenticationTag, plaintextBytes, Encoding.UTF8.GetBytes(secret.FieldName));
            return Encoding.UTF8.GetString(plaintextBytes);
        }
        finally
        {
            CryptographicOperations.ZeroMemory(dataKey);
            if (plaintextBytes is not null) CryptographicOperations.ZeroMemory(plaintextBytes);
        }
    }

}
