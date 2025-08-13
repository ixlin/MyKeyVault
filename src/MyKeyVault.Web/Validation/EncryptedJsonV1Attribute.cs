using System.ComponentModel.DataAnnotations;
using System.Text.Json;

namespace MyKeyVault.Web.Validation;

public class EncryptedJsonV1Attribute : ValidationAttribute
{
    protected override ValidationResult? IsValid(object? value, ValidationContext validationContext)
    {
        if (value == null) return ValidationResult.Success;
        var s = value as string;
        if (string.IsNullOrWhiteSpace(s)) return ValidationResult.Success; // allow empty unless [Required]
        try
        {
            using var doc = JsonDocument.Parse(s);
            var root = doc.RootElement;
            if (!root.TryGetProperty("v", out var v) || v.GetString() != "v1")
                return new ValidationResult("Invalid ciphertext version.");
            if (!root.TryGetProperty("alg", out var alg) || alg.GetString() != "AES-GCM")
                return new ValidationResult("Invalid ciphertext alg.");
            if (!root.TryGetProperty("iv", out var _) || !root.TryGetProperty("ct", out var _))
                return new ValidationResult("Invalid ciphertext payload.");
            return ValidationResult.Success;
        }
        catch
        {
            return new ValidationResult("Encrypted field must be v1 ciphertext JSON.");
        }
    }
}
