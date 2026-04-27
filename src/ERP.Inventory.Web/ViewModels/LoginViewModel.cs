using System.ComponentModel.DataAnnotations;

namespace ERP.Inventory.Web.ViewModels;

public sealed class LoginViewModel
{
    [Required]
    public string UserName { get; set; } = string.Empty;

    [Required]
    [DataType(DataType.Password)]
    public string Password { get; set; } = string.Empty;

    public bool RememberMe { get; set; }
    public string LanguageCode { get; set; } = "vi";
    public string? ReturnUrl { get; set; }
}

