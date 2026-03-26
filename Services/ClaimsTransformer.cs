using Microsoft.AspNetCore.Authentication;
using System.Security.Claims;
using KEYREGISTERAUTOMATION.Data;
using Microsoft.EntityFrameworkCore;

public class ClaimsTransformer : IClaimsTransformation
{
    private readonly ApplicationDbContext _context;

    public ClaimsTransformer(ApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<ClaimsPrincipal> TransformAsync(ClaimsPrincipal principal)
    {
        if (!principal.Identity?.IsAuthenticated ?? true)
            return principal;

        var windowsName = principal.Identity.Name;

        if (string.IsNullOrEmpty(windowsName))
            return principal;

        var igg = windowsName.Split('\\').Last().ToUpper();

        var user = await _context.UserAccounts
        .FirstOrDefaultAsync(u => u.IGG.ToUpper() == igg);

        if (user == null)
            return principal;

        var identity = new ClaimsIdentity("ApplicationRole");

        identity.AddClaim(new Claim(ClaimTypes.Role, user.Role));
        identity.AddClaim(new Claim("IGG", user.IGG));

        principal.AddIdentity(identity);

        return principal;
    }
}
