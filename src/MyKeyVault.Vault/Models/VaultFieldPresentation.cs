namespace MyKeyVault.Vault.Models;

public enum VaultFieldColumn
{
    Account,
    Password,
    Note
}

public static class VaultFieldPresentation
{
    private static readonly string[] AccountTerms = ["账号", "账户", "用户名", "邮箱", "卡号", "持卡人"];
    private static readonly string[] PasswordTerms = ["密码", "密钥", "api key", "token", "secret", "安全码", "cvv", "cvc", "pin"];

    public static VaultFieldColumn Classify(string fieldName)
    {
        var normalized = fieldName.Trim().ToLowerInvariant();
        if (AccountTerms.Any(normalized.Contains)) return VaultFieldColumn.Account;
        if (PasswordTerms.Any(normalized.Contains)) return VaultFieldColumn.Password;
        return VaultFieldColumn.Note;
    }

    public static bool CanCopy(string fieldName) => Classify(fieldName) is VaultFieldColumn.Account or VaultFieldColumn.Password;
}
