using Microsoft.AspNetCore.Identity;

namespace GymManagementSystem.Data;

public static class DbInitializer
{
    public static async Task SeedRolesAsync(RoleManager<IdentityRole> roleManager)
    {
        string[] roles = ["Admin", "Member", "Trainer"];

        foreach (var role in roles)
        {
            if (!await roleManager.RoleExistsAsync(role))
            {
                await roleManager.CreateAsync(new IdentityRole(role));
            }
        }
    }
}
