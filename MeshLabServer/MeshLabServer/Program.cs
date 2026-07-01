using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.Server.Kestrel.Core;
using Microsoft.AspNetCore.StaticFiles;
using Microsoft.Extensions.FileProviders;
using Serilog;

namespace MeshLabServer
{
    public class Program
    {
        public static void Main(string[] args)
        {
            var builder = WebApplication.CreateBuilder(args);

            var isLinuxPlatform = System.Runtime.InteropServices.RuntimeInformation.IsOSPlatform(System.Runtime.InteropServices.OSPlatform.Linux);
            var FileStorage = isLinuxPlatform ? "FileStorageLinux" : "FileStorage";

            // Create folders for models and logs
            var modelsDir = builder.Configuration[$"{FileStorage}:ModelsDir"];
            if (string.IsNullOrEmpty(modelsDir))
            {
                throw new InvalidOperationException("Models path is not configured.");
            }
            Directory.CreateDirectory(modelsDir);

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

            // Add services to the container.
            builder.Services.AddControllersWithViews();

            builder.Services.AddRazorPages();
            builder.Services.AddDirectoryBrowser();

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
            if (!System.Runtime.InteropServices.RuntimeInformation.IsOSPlatform(System.Runtime.InteropServices.OSPlatform.Linux))
            {
                app.UseHttpsRedirection();
            }

            app.UseStaticFiles();

            var extensionProvider = new FileExtensionContentTypeProvider();
            extensionProvider.Mappings.Add(".py", "application/octet-stream");

            app.UseStaticFiles(new StaticFileOptions
            {
                FileProvider = new PhysicalFileProvider(
                    Path.Combine(builder.Environment.WebRootPath, "python")),
                RequestPath = "/python",
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

            app.MapStaticAssets();
            app.MapControllerRoute(
                name: "default",
                pattern: "{controller=Home}/{action=Index}/{id?}")
                .WithStaticAssets();

            app.Run();
        }
    }
}
