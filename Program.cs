using KEYREGISTERAUTOMATION.Data;
using KEYREGISTERAUTOMATION.Models;
using KEYREGISTERAUTOMATION.Services;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Negotiate;
using Microsoft.EntityFrameworkCore;
using System;


var builder = WebApplication.CreateBuilder(args);

var connectionString = builder.Configuration.GetConnectionString("DefaultConnection");

builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseSqlServer(connectionString, sqlServerOptions =>
    {
        sqlServerOptions.EnableRetryOnFailure(
            maxRetryCount: 5,
            maxRetryDelay: TimeSpan.FromSeconds(30),
            errorNumbersToAdd: null);
    }));

builder.Services.AddAuthentication(NegotiateDefaults.AuthenticationScheme)
    .AddNegotiate();/*options =>
    {
        options.PersistKerberosCredentials = true;
    });*/

builder.Services.AddAuthorization(options =>
{
    options.AddPolicy("AdministratorOnly",
        policy => policy.RequireRole("Administrator"));

    options.AddPolicy("FacilityManagerOnly",
        policy => policy.RequireRole("Facility Manager"));
});

builder.Services.AddScoped<IClaimsTransformation, ClaimsTransformer>();


builder.Services.Configure<EmailSettings>(
    builder.Configuration.GetSection("EmailSettings"));

builder.Services.AddScoped<IEmailService, EmailService>();


builder.Services.AddControllersWithViews();

var app = builder.Build();

app.UseHttpsRedirection();
app.UseStaticFiles();

app.UseRouting();

app.UseAuthentication();  
app.UseAuthorization();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");

app.Run();

