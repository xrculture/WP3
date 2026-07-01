using System.ComponentModel.DataAnnotations;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using XRCultureHub.Services;

namespace XRCultureHub.Pages.Account
{
    [AllowAnonymous]
    public class LoginModel : PageModel
    {
        private readonly ILogger<LoginModel> _logger;
        private readonly IConfiguration _configuration;
        private readonly ITokenService _tokenService;

        public LoginModel(ILogger<LoginModel> logger, IConfiguration configuration, ITokenService tokenService)
        {
            _logger = logger;
            _configuration = configuration;
            _tokenService = tokenService;
        }

        [BindProperty]
        public LoginInputModel LoginInput { get; set; } = new();

        [BindProperty(SupportsGet = true)]
        public string ReturnUrl { get; set; } = "/";

        public string? ErrorMessage { get; set; }

        public void OnGet(string returnUrl = "")
        {
            if (!string.IsNullOrEmpty(ErrorMessage))
            {
                ModelState.AddModelError(string.Empty, ErrorMessage);
            }

            ReturnUrl = returnUrl ?? Url.Content("~/");
        }

        public async Task<IActionResult> OnPostAsync()
        {
            if (ModelState.IsValid && !string.IsNullOrEmpty(LoginInput.Username) && !string.IsNullOrEmpty(LoginInput.Password))
            {
                if (IsValidUser(LoginInput.Username, LoginInput.Password))
                {
                    // Generate token with user ID, name, and roles
                    var token = _tokenService.GenerateToken(
                        "user123", // #todo: user's ID?
                        LoginInput.Username,
                        ["User"]
                    );

                    var handler = new JwtSecurityTokenHandler();
                    var jwtToken = handler.ReadJwtToken(token);
                    var jwtId = jwtToken.Id;

                    var refreshToken = _tokenService.GenerateRefreshToken("user123", jwtId);// #todo: user's ID?

                    // Store both tokens
                    Response.Cookies.Append("jwt_token", token, new CookieOptions
                    {
                        HttpOnly = true,
                        Secure = true,
                        SameSite = SameSiteMode.Strict,
                        Expires = DateTime.UtcNow.AddMinutes(60) // Match your token expiry
                    });

                    Response.Cookies.Append("refresh_token", refreshToken.Token, new CookieOptions
                    {
                        HttpOnly = true,
                        Secure = true,
                        SameSite = SameSiteMode.Strict,
                        Expires = DateTime.UtcNow.AddDays(7)
                    });

                    var claims = new List<Claim>
                    {
                        new Claim(ClaimTypes.Name, LoginInput.Username),
                        // Add additional claims as needed
                        new Claim(ClaimTypes.Role, "User")
                    };

                    var claimsIdentity = new ClaimsIdentity(claims, "XRCultureHubCookieAuth");
                    var authProperties = new AuthenticationProperties
                    {
                        IsPersistent = LoginInput.RememberMe,
                        ExpiresUtc = DateTimeOffset.UtcNow.AddDays(14)
                    };

                    await HttpContext.SignInAsync(
                        "XRCultureHubCookieAuth",
                        new ClaimsPrincipal(claimsIdentity),
                        authProperties);

                    _logger.LogInformation("User {Username} logged in at {Time}.",
                        LoginInput.Username, DateTime.UtcNow);

                    if (Url.IsLocalUrl(ReturnUrl))
                    {
                        return LocalRedirect(ReturnUrl);
                    }

                    return RedirectToPage("/Index");
                }

                ModelState.AddModelError(string.Empty, "Invalid login attempt.");
                return Page();
            }

            // If we got this far, something failed, redisplay form
            return Page();
        }

        private bool IsValidUser(string? username, string? password)
        {
            if (_configuration == null)
            {
                _logger.LogError("Configuration is not set.");
                return false;
            }

            var isLinuxPlatform = System.Runtime.InteropServices.RuntimeInformation.IsOSPlatform(System.Runtime.InteropServices.OSPlatform.Linux);
            var FileStorage = isLinuxPlatform ? "FileStorageLinux" : "FileStorage";

            var credentialsDir = _configuration[$"{FileStorage}:CredentialsDir"];
            if (string.IsNullOrEmpty(credentialsDir))
            {
                _logger.LogError("Credentials path is not configured.");
                return false;
            }

            if (string.IsNullOrEmpty(username) || string.IsNullOrEmpty(password))
            {
                return false;
            }

            return ValidateFromFile(username, password);
        }

        private bool ValidateFromFile(string username, string password)
        {
            var isLinuxPlatform = System.Runtime.InteropServices.RuntimeInformation.IsOSPlatform(System.Runtime.InteropServices.OSPlatform.Linux);
            var FileStorage = isLinuxPlatform ? "FileStorageLinux" : "FileStorage";

            var credentialsDir = _configuration[$"{FileStorage}:CredentialsDir"];
            if (string.IsNullOrEmpty(credentialsDir) || !Directory.Exists(credentialsDir))
            {
                return false;
            }

            var credentialsPath = Path.Combine(credentialsDir, "credentials.txt");
            if (!System.IO.File.Exists(credentialsPath))
            {
                return false;
            }

            try
            {
                var lines = System.IO.File.ReadAllLines(credentialsPath);
                foreach (var line in lines)
                {
                    if (string.IsNullOrWhiteSpace(line) || line.StartsWith("#"))
                        continue;

                    var parts = line.Split(':', 2);
                    if (parts.Length == 2)
                    {
                        var fileUsername = parts[0].Trim();
                        var hashedPassword = parts[1].Trim();

                        if (fileUsername == username && BCrypt.Net.BCrypt.Verify(password, hashedPassword))
                        {
                            return true;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error reading credentials file");
            }

            return false;
        }
    }

    public class LoginInputModel
    {
        [Required]
        public string? Username { get; set; }

        [Required]
        [DataType(DataType.Password)]
        public string? Password { get; set; }

        [Display(Name = "Remember me")]
        public bool RememberMe { get; set; }
    }
}