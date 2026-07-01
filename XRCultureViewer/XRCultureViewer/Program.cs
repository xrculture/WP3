using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.Server.Kestrel.Core;
using Microsoft.AspNetCore.StaticFiles;
using Microsoft.Extensions.FileProviders;
using Serilog;
using XRCultureViewer.Services;

public class Program
{
    public static void Main(string[] args)
    {
        // Must be set before Playwright initialises its driver path resolution.
        var isLinuxPlatform = System.Runtime.InteropServices.RuntimeInformation
            .IsOSPlatform(System.Runtime.InteropServices.OSPlatform.Linux);

        var tempBuilder = WebApplication.CreateBuilder(args);
        var browsersPathKey = isLinuxPlatform
            ? "ThumbnailService:BrowsersPathLinux"
            : "ThumbnailService:BrowsersPath";
        var browsersPath = tempBuilder.Configuration[browsersPathKey];
        if (!string.IsNullOrWhiteSpace(browsersPath))
            Environment.SetEnvironmentVariable("PLAYWRIGHT_BROWSERS_PATH", browsersPath);

        var builder = WebApplication.CreateBuilder(args);

        var FileStorage = isLinuxPlatform ? "FileStorageLinux" : "FileStorage";

        // Create folders for models and logs
        var modelsDir = builder.Configuration[$"{FileStorage}:ModelsDir"];
        if (string.IsNullOrEmpty(modelsDir))
        {
            throw new InvalidOperationException("Models path is not configured.");
        }
        Directory.CreateDirectory(modelsDir);

        var thumbnailsDir = builder.Configuration[$"{FileStorage}:ThumbnailsDir"];
        if (string.IsNullOrEmpty(thumbnailsDir))
        {
            throw new InvalidOperationException("Thumbnails path is not configured.");
        }
        Directory.CreateDirectory(thumbnailsDir);

        var logsDir = builder.Configuration[$"{FileStorage}:LogsDir"];
        if (string.IsNullOrEmpty(logsDir))
        {
            throw new InvalidOperationException("Logs path is not configured.");
        }
        Directory.CreateDirectory(logsDir);

        // Serilog
        builder.Host.UseSerilog((context, services, configuration) => configuration
            .ReadFrom.Configuration(context.Configuration)
            .WriteTo.File(Path.Combine(logsDir, "log.txt"), rollingInterval: RollingInterval.Day)
        );

        builder.Services.AddControllersWithViews();

        builder.Services.AddAuthentication(options =>
        {
            options.DefaultScheme = "XRCultureViewerCookieAuth";
            options.DefaultChallengeScheme = "XRCultureViewerCookieAuth";
        })
        .AddCookie("XRCultureViewerCookieAuth", options =>
        {
            options.LoginPath = "/Account/Login";
            options.LogoutPath = "/Account/Logout";
            options.AccessDeniedPath = "/Account/AccessDenied";
            options.ReturnUrlParameter = "returnUrl";
            options.ExpireTimeSpan = TimeSpan.FromDays(14);
            options.Cookie.SameSite = SameSiteMode.Lax;
            options.Cookie.SecurePolicy = CookieSecurePolicy.None;
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
            options.Conventions.AllowAnonymousToPage("/Viewer");
            options.Conventions.AllowAnonymousToPage("/GaussianSplattingViewer");
            options.Conventions.AllowAnonymousToPage("/Storage");
            options.Conventions.AllowAnonymousToPage("/ThumbnailGenerator");

            options.Conventions.AllowAnonymousToPage("/Account/Login");
            options.Conventions.AllowAnonymousToPage("/Account/Logout");
            options.Conventions.AllowAnonymousToPage("/Account/AccessDenied");

            options.Conventions.AllowAnonymousToPage("/Oembed");
        });

        builder.Services.Configure<IISServerOptions>(options =>
        {
            options.MaxRequestBodySize = 2_147_483_648; // 2GB
        });

        builder.Services.Configure<KestrelServerOptions>(options =>
        {
            options.Limits.MaxRequestBodySize = 2_147_483_648; // 2GB
        });

        // For form uploads specifically
        builder.Services.Configure<FormOptions>(options =>
        {
            options.MultipartBodyLengthLimit = 2_147_483_648; // 2GB
            options.ValueLengthLimit = 500 * 1024 * 1024;     // 1GB
        });

        builder.Services.AddDirectoryBrowser();
        builder.Services.AddHttpContextAccessor();

        // Add HttpClient services
        builder.Services.AddHttpClient();

        // Model loading service
        builder.Services.AddScoped<IModelLoaderService, ModelLoaderService>();

        // Thumbnail generation background service
        builder.Services.AddSingleton<ThumbnailBackgroundService>();
        builder.Services.AddSingleton<IThumbnailService>(sp => sp.GetRequiredService<ThumbnailBackgroundService>());
        builder.Services.AddHostedService(sp => sp.GetRequiredService<ThumbnailBackgroundService>());

        var app = builder.Build();

        // Configure the HTTP request pipeline.
        if (!app.Environment.IsDevelopment())
        {
            app.UseExceptionHandler("/Home/Error");
            app.UseHsts();
        }

        // Don't redirect to HTTPS on Linux (Docker) - requires certificates to be set up, which is not common in development environments.
        // In production, HTTPS should be handled by a reverse proxy like Nginx or Traefik.
        if (!isLinuxPlatform)
        {
            app.UseHttpsRedirection();
        }

        app.UseStaticFiles();

        // Custom MIME types and other configuration
        var extensionProvider = new FileExtensionContentTypeProvider();
        extensionProvider.Mappings.Add(".data", "application/octet-stream");
        extensionProvider.Mappings.Add(".binz", "application/octet-stream");
        extensionProvider.Mappings.Add(".splat", "application/octet-stream");
        extensionProvider.Mappings.Add(".ply", "application/octet-stream");
        extensionProvider.Mappings.Add(".ifc", "text/plain");
        extensionProvider.Mappings.Add(".step", "text/plain");
        extensionProvider.Mappings.Add(".stp", "text/plain");

        // Then protected viewer content only
        app.UseStaticFiles(new StaticFileOptions
        {
            FileProvider = new PhysicalFileProvider(Path.Combine(app.Environment.WebRootPath, "viewer")),
            RequestPath = "/viewer",
            ContentTypeProvider = extensionProvider,
            ServeUnknownFileTypes = true,
            OnPrepareResponse = ctx =>
            {
                ctx.Context.Response.Headers["Cache-Control"] = "no-cache, no-store, must-revalidate";
                ctx.Context.Response.Headers["Pragma"] = "no-cache";
                ctx.Context.Response.Headers["Expires"] = "0";
            }
        });

        app.UseRouting();
        app.UseAuthentication();
        app.UseAuthorization();
        app.MapRazorPages();

        app.Run();
    }
}