using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.Server.Kestrel.Core;
using Microsoft.AspNetCore.StaticFiles;
using Microsoft.Extensions.FileProviders;
using Serilog;
using XRCulture3DReconstruction.Hubs;
using XRCulture3DReconstruction.Services;

namespace XRCulture3DReconstruction
{
    public class Program
    {
        public static void Main(string[] args)
        {
            var builder = WebApplication.CreateBuilder(args);

            var isLinuxPlatform = System.Runtime.InteropServices.RuntimeInformation.IsOSPlatform(System.Runtime.InteropServices.OSPlatform.Linux);
            var fileStorage = isLinuxPlatform ? "FileStorageLinux" : "FileStorage";

            var modelsDir = builder.Configuration[$"{fileStorage}:ModelsDir"];
            if (string.IsNullOrEmpty(modelsDir))
            {
                throw new InvalidOperationException("Models path is not configured.");
            }
            Directory.CreateDirectory(modelsDir);

            var tasksDir = builder.Configuration[$"{fileStorage}:TasksDir"];
            if (string.IsNullOrEmpty(tasksDir))
            {
                throw new InvalidOperationException("Tasks path is not configured.");
            }
            Directory.CreateDirectory(tasksDir);

            var logsDir = builder.Configuration[$"{fileStorage}:LogsDir"];
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

            builder.Services.AddAuthentication(options =>
            {
                options.DefaultScheme = "XRCulture3DReconstructionCookieAuth";
                options.DefaultChallengeScheme = "XRCulture3DReconstructionCookieAuth";
            })
            .AddCookie("XRCulture3DReconstructionCookieAuth", options =>
            {
                options.LoginPath = "/Account/Login";
                options.LogoutPath = "/Account/Logout";
                options.AccessDeniedPath = "/Account/AccessDenied";
                options.ReturnUrlParameter = "returnUrl";
                options.ExpireTimeSpan = TimeSpan.FromDays(14);
                options.Cookie.SameSite = SameSiteMode.Lax;
                options.Cookie.SecurePolicy = CookieSecurePolicy.None;
            }).AddNegotiate();
            builder.Services.AddHttpContextAccessor();

            builder.Services.AddAuthorization(options =>
            {
                // By default, all incoming requests will be authorized according to the default policy.
                options.FallbackPolicy = options.DefaultPolicy;
            });

            // Common Services
            builder.Services.AddTransient<IOperationTransient, Operation>();
            builder.Services.AddScoped<IOperationScoped, Operation>();
            builder.Services.AddSingleton<IOperationSingleton, Operation>();
            builder.Services.AddSingleton<IOperationSingletonInstance>(new Operation(Guid.Empty));
            builder.Services.AddTransient<OperationService, OperationService>();

            // SignalR Services
            builder.Services.AddSignalR(options =>
            {
                options.EnableDetailedErrors = true;
                options.KeepAliveInterval = TimeSpan.FromSeconds(15);
                options.ClientTimeoutInterval = TimeSpan.FromSeconds(60);
                options.HandshakeTimeout = TimeSpan.FromSeconds(15);
                options.MaximumReceiveMessageSize = null; // Unlimited message size
            });
            builder.Services.AddSingleton<IConnectionStateService, ConnectionStateService>();
            builder.Services.AddSingleton<ISignalRLoggerService, SignalRLoggerService>();
            builder.Services.AddSingleton<ISignalRStatusService, SignalRStatusService>();

            // Log
            builder.Services.AddSingleton<TaskLoggerFactory>();

            // SignalR Authorization
            builder.Services.AddAuthorization(options =>
            {
                options.AddPolicy("AllowAnonymousSignalR", policy =>
                {
                    policy.RequireAssertion(context => true); // Always allow
                });

                // By default, all incoming requests will be authorized according to the default policy.
                options.FallbackPolicy = options.DefaultPolicy;
            });

            // Services
            builder.Services.AddRazorPages(options =>
            {
                if (isLinuxPlatform)
                {
                    // Allow anonymous access to the Index page on Linux (Docker)
                    //#todo Remove when Authentication is fully implemented
                    options.Conventions.AllowAnonymousToPage("/Index");
                    options.Conventions.AllowAnonymousToPage("/Library");
                }

                //#todo Remove when Authentication is fully implemented
                options.Conventions.AllowAnonymousToPage("/logHub");
                options.Conventions.AllowAnonymousToPage("/statusHub");
                options.Conventions.AllowAnonymousToPage("/Viewer");
                options.Conventions.AllowAnonymousToPage("/Storage");
                options.Conventions.AllowAnonymousToPage("/TaskManager");

                options.Conventions.AllowAnonymousToPage("/Account/Login");
                options.Conventions.AllowAnonymousToPage("/Account/Logout");
                options.Conventions.AllowAnonymousToPage("/Account/AccessDenied");
            });

            builder.Services.AddHostedService<FileWatcherService>();

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

            var app = builder.Build();

            // Force eager initialization of the singletons so that their constructors
            // are called immediately rather than lazily.
            app.Services.GetRequiredService<ISignalRStatusService>();            

            // SignalR Hubs
            app.MapHub<LogHub>("/logHub");
            app.MapHub<StatusHub>("/statusHub");
            app.MapHub<LibraryHub>("/hubs/library");

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

            var extensionProvider = new FileExtensionContentTypeProvider();
            extensionProvider.Mappings.Add(".data", "application/octet-stream");
            extensionProvider.Mappings.Add(".binz", "application/octet-stream");

            app.UseStaticFiles(new StaticFileOptions
            {
                FileProvider = new PhysicalFileProvider(Path.Combine(builder.Environment.WebRootPath, "viewer")),
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

            app.UseStaticFiles(new StaticFileOptions
            {
                FileProvider = new PhysicalFileProvider(Path.Combine(builder.Environment.WebRootPath, "data")),
                RequestPath = "/data",
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
}
