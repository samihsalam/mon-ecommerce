using Microsoft.AspNetCore.Identity;

namespace MonEcommerce.Infrastructure.Identity;

public class ApplicationUser : IdentityUser
{
    public string Name { get; set; } = string.Empty;
}
