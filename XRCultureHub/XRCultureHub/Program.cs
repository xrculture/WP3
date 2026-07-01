using Microsoft.AspNetCore.Server.Kestrel.Core;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using System.Text;
using Serilog;
using XRCultureHub.Models;
using XRCultureHub.Services;

namespace XRCultureHub
{
    public class Program
    {
        public static void Main(string[] args)
        {
            var builder = WebApplication.CreateBuilder(args);

            var isLinuxPlatform = System.Runtime.InteropServices.RuntimeInformation.IsOSPlatform(System.Runtime.InteropServices.OSPlatform.Linux);
            var FileStorage = isLinuxPlatform ? "FileStorageLinux" : "FileStorage";

            // Create folders for logs, viewers, and services
            var viewersDir = builder.Configuration[$"{FileStorage}:ViewersDir"];
            if (string.IsNullOrEmpty(viewersDir))
            {
                throw new InvalidOperationException("Viewers path is not configured.");
            }
            Directory.CreateDirectory(viewersDir);

            var servicesDir = builder.Configuration[$"{FileStorage}:ServicesDir"];
            if (string.IsNullOrEmpty(servicesDir))
            {
                throw new InvalidOperationException("Services path is not configured.");
            }
            Directory.CreateDirectory(servicesDir);

            var logsDir = builder.Configuration[$"{FileStorage}:LogsDir"];
            if (string.IsNullOrEmpty(logsDir))
            {
                throw new InvalidOperationException("Logs path is not configured.");
            }
            Directory.CreateDirectory(logsDir);

            var jwtSecretKey = builder.Configuration["JwtSettings:SecretKey"];
            if (string.IsNullOrEmpty(jwtSecretKey))
            {
                throw new InvalidOperationException("JWT Secret Key is not configured.");
            }

            // Serilog
            builder.Host.UseSerilog((context, services, configuration) => configuration
                .ReadFrom.Configuration(context.Configuration)
                .WriteTo.File(Path.Combine(logsDir, "log.txt"), rollingInterval: RollingInterval.Day)
            );

            builder.Services.AddControllersWithViews();

            builder.Services.AddTransient<IOperationTransient, Operation>();
            builder.Services.AddScoped<IOperationScoped, Operation>();
            builder.Services.AddSingleton<IOperationSingleton, Operation>();
            builder.Services.AddSingleton<IOperationSingletonInstance>(new Operation(Guid.Empty));
            builder.Services.AddTransient<OperationService, OperationService>();

            builder.Services.AddSingleton<IRefreshTokenRepository, InMemoryRefreshTokenRepository>();
            builder.Services.AddScoped<ITokenService, TokenService>();
            builder.Services.Configure<JwtSettings>(builder.Configuration.GetSection("JwtSettings"));

            builder.Services.AddAuthentication(options =>
            {
                // Cookie authentication as default for browser flows
                options.DefaultScheme = "XRCultureHubCookieAuth";
                options.DefaultSignInScheme = "XRCultureHubCookieAuth";
                options.DefaultChallengeScheme = "XRCultureHubCookieAuth";

                // JWT for API authentication
            })
            .AddCookie("XRCultureHubCookieAuth", options =>
            {
                options.LoginPath = "/Account/Login";
                options.LogoutPath = "/Account/Logout";
                options.AccessDeniedPath = "/Account/AccessDenied";
                options.ReturnUrlParameter = "returnUrl";
                options.ExpireTimeSpan = TimeSpan.FromDays(14);
                options.Cookie.SameSite = SameSiteMode.Lax;
                options.Cookie.SecurePolicy = CookieSecurePolicy.None;
            })
            .AddJwtBearer(options =>
            {
                // JWT configuration for API authentication
                options.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidateIssuer = true,
                    ValidateAudience = true,
                    ValidateLifetime = true,
                    ValidateIssuerSigningKey = true,
                    ValidIssuer = builder.Configuration["JwtSettings:Issuer"],
                    ValidAudience = builder.Configuration["JwtSettings:Audience"],
                    IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtSecretKey))
                };
            }).AddNegotiate();

            builder.Services.AddAuthorization(options =>
            {
                // By default, all incoming requests will be authorized according to the default policy.
                options.FallbackPolicy = options.DefaultPolicy;
            });

            builder.Services.AddRazorPages(options =>
            {
                if (isLinuxPlatform)
                {
                    // Allow anonymous access to the Index page on Linux (Docker)
                    //#todo Remove when Authentication is fully implemented
                    options.Conventions.AllowAnonymousToPage("/Index");
                }

                //#todo Remove when Authentication is fully implemented
                options.Conventions.AllowAnonymousToPage("/Registry");

                options.Conventions.AllowAnonymousToPage("/Account/Login");
                options.Conventions.AllowAnonymousToPage("/Account/Logout");
                options.Conventions.AllowAnonymousToPage("/Account/AccessDenied");
            });

            builder.Services.Configure<IISServerOptions>(options =>
            {
                options.MaxRequestBodySize = 100 * 1024 * 1024; // 100MB
            });

            builder.Services.Configure<KestrelServerOptions>(options =>
            {
                options.Limits.MaxRequestBodySize = 100 * 1024 * 1024; // 100MB
            });

            builder.Services.AddDirectoryBrowser();
            builder.Services.AddHttpContextAccessor();

            var app = builder.Build();

            // Configure the HTTP request pipeline.
            if (!app.Environment.IsDevelopment())
            {
                app.UseExceptionHandler("/Home/Error");
            }
            else
            {
                app.UseHsts();
            }

            // Don't redirect to HTTPS on Linux (Docker) - requires certificates to be set up, which is not common in development environments.
            // In production, HTTPS should be handled by a reverse proxy like Nginx or Traefik.
            if (!isLinuxPlatform)
            {
                app.UseHttpsRedirection();
            }

            app.UseStaticFiles();

            app.UseRouting();
            app.UseAuthentication();
            app.UseAuthorization();
            app.MapRazorPages();

            app.Run();
        }
    }
}
