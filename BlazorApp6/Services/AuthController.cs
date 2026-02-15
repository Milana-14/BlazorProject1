using BlazorApp6.Services;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace BlazorApp6.Controllers;

[Route("account")]
public class AccountController : Controller
{
    private readonly StudentManager _studentManager;
    private readonly ILogger<AccountController> _logger;

    public AccountController(StudentManager studentManager, ILogger<AccountController> logger)
    {
        _studentManager = studentManager;
        _logger = logger;
    }

    [HttpPost("login")]
    public async Task<IActionResult> Login(
        [FromForm] string username,
        [FromForm] string password,
        [FromForm] string? returnUrl = null)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(username) || string.IsNullOrWhiteSpace(password))
            {
                return Redirect("/login?errorMessage=" + Uri.EscapeDataString("Моля, попълнете всички полета."));
            }

            var student = await _studentManager.FindStudentByUsername(username);

            if (student == null)
            {
                _logger.LogWarning("Login attempt for non-existent user: {Username}", username);
                return Redirect("/login?errorMessage=" + Uri.EscapeDataString("Невалиден юзърнейм или парола."));
            }

            if (!HashPasswordService.ComparePasswords(student.Password, password))
            {
                _logger.LogWarning("Failed login attempt for user: {Username}", username);
                return Redirect("/login?errorMessage=" + Uri.EscapeDataString("Невалиден юзърнейм или парола."));
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

            _logger.LogInformation("User {Username} logged in successfully", student.Username);

            return Redirect(returnUrl ?? "/my-profile");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during login for user: {Username}", username);
            return Redirect("/login?errorMessage=" + Uri.EscapeDataString("Възникна грешка. Моля, опитайте отново."));
        }
    }

    [HttpPost("logout")]
    public async Task<IActionResult> Logout(string? returnUrl = null)
    {
        await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
        _logger.LogInformation("User logged out");
        return Redirect(returnUrl ?? "/");
    }
}