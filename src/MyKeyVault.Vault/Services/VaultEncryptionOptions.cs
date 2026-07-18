namespace MyKeyVault.Vault.Services;

public sealed class VaultEncryptionOptions
{
    public const string SectionName = "VaultEncryption";
    public string MasterKey { get; init; } = string.Empty;
}
