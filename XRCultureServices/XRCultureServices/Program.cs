using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.Server.Kestrel.Core;
using Microsoft.AspNetCore.StaticFiles;
using Microsoft.Extensions.FileProviders;
using Serilog;
using XRCultureServices.Services;

namespace XRCultureServices
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

            builder.Services.AddHttpContextAccessor();

            // Common Services
            builder.Services.AddTransient<IOperationTransient, Operation>();
            builder.Services.AddScoped<IOperationScoped, Operation>();
            builder.Services.AddSingleton<IOperationSingleton, Operation>();
            builder.Services.AddSingleton<IOperationSingletonInstance>(new Operation(Guid.Empty));
            builder.Services.AddTransient<OperationService, OperationService>();

            // Services
            builder.Services.AddRazorPages(options =>
            {
                //#todo Remove when Authentication is fully implemented
                options.Conventions.AllowAnonymousToPage("/Index");
                options.Conventions.AllowAnonymousToPage("/Convert");

                //options.Conventions.AllowAnonymousToPage("/Account/Login");
                //options.Conventions.AllowAnonymousToPage("/Account/Logout");
                //options.Conventions.AllowAnonymousToPage("/Account/AccessDenied");
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

            builder.Services.AddHttpContextAccessor();
            builder.Services.AddHttpClient();

            var app = builder.Build();

            // Configure the HTTP request pipeline.
            if (!app.Environment.IsDevelopment())
            {
                app.UseExceptionHandler("/Error");
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
