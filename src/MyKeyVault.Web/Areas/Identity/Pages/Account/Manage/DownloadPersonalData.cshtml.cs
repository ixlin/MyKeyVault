using System.Text.Json;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using MyKeyVault.Web.Models;

namespace MyKeyVault.Web.Areas.Identity.Pages.Account.Manage;

public class DownloadPersonalDataModel : PageModel
{
    private readonly UserManager<ApplicationUser> _userManager;
    public DownloadPersonalDataModel(UserManager<ApplicationUser> userManager){_userManager=userManager;}
    public async Task<IActionResult> OnPostAsync(){ var u=await _userManager.GetUserAsync(User); if(u==null) return NotFound(); var data=new Dictionary<string,object?>{ ["Id"]=u.Id, ["Email"]=u.Email, ["PhoneNumber"]=u.PhoneNumber, ["CreatedAt"]=u.CreatedAt, ["LastLoginAt"]=u.LastLoginAt}; var bytes=System.Text.Encoding.UTF8.GetBytes(JsonSerializer.Serialize(data, new JsonSerializerOptions{WriteIndented=true})); return File(bytes, "application/json", "personal-data.json"); }
}
