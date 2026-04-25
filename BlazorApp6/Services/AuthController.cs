using BlazorApp6.Services;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace BlazorApp6.Controllers;

[Route("account")]
public class AccountController : Controller
{
    private readonly StudentManager studentManager;
    private readonly ILogger<AccountController> logger;

    public AccountController(StudentManager studentManager, ILogger<AccountController> logger)
    {
        this.studentManager = studentManager;
        this.logger = logger;
    }

    [HttpPost("login")]
    public async Task<IActionResult> Login([FromForm] string username, [FromForm] string password, [FromForm] string? returnUrl = null)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(username) || string.IsNullOrWhiteSpace(password))
            {
                return Redirect("/login?errorMessage=" + Uri.EscapeDataString("Моля, попълнете всички полета."));
            }

            var student = studentManager.FindStudent(s => s.Username == username);

            if (student == null)
            {
                logger.LogWarning("Login attempt for non-existent user: {Username}", username);
                return Redirect("/login?errorMessage=" + Uri.EscapeDataString("Невалидно потребителско име или парола."));
            }

            if (!HashPasswordService.ComparePasswords(student.Password, password))
            {
                logger.LogWarning("Failed login attempt for user: {Username}", username);
                return Redirect("/login?errorMessage=" + Uri.EscapeDataString("Невалидно потребителско име или парола."));
            }

            var claims = new List<Claim>
            {
                new Claim(ClaimTypes.NameIdentifier, student.Id.ToString()),
                new Claim(ClaimTypes.Name, student.Username),
                new Claim(ClaimTypes.Role, "User")
            };

            var identity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
            var principal = new ClaimsPrincipal(identity);

            var authProperties = new AuthenticationProperties
            {
                IsPersistent = true,
                ExpiresUtc = DateTimeOffset.UtcNow.AddDays(7),
                RedirectUri = returnUrl ?? "/my-profile"
            };

            await HttpContext.SignInAsync(
                CookieAuthenticationDefaults.AuthenticationScheme,
                principal,
                authProperties);

            logger.LogInformation("User {Username} logged in successfully", student.Username);

            return Redirect(returnUrl ?? "/my-profile");
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error during login for user: {Username}", username);
            return Redirect("/login?errorMessage=" + Uri.EscapeDataString("Възникна грешка. Моля, опитайте отново."));
        }
    }

    [HttpPost("logout")]
    public async Task<IActionResult> Logout(string? returnUrl = null)
    {
        await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
        logger.LogInformation("User logged out");
        return Redirect(returnUrl ?? "/");
    }
}