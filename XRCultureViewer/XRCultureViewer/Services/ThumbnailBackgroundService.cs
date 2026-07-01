using System.Collections.Concurrent;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.Playwright;

namespace XRCultureViewer.Services;

public sealed class ThumbnailBackgroundService : BackgroundService, IThumbnailService
{
    private readonly ConcurrentQueue<string> _queue = new();
    private readonly SemaphoreSlim _signal = new(0);
    private readonly IConfiguration _configuration;
    private readonly ILogger<ThumbnailBackgroundService> _logger;
    private readonly IServer _server;
    private readonly IHostApplicationLifetime _lifetime;

    public ThumbnailBackgroundService(
        IConfiguration configuration,
        ILogger<ThumbnailBackgroundService> logger,
        IServer server,
        IHostApplicationLifetime lifetime)
    {
        _configuration = configuration;
        _logger = logger;
        _server = server;
        _lifetime = lifetime;
    }

    public void Enqueue(string modelFileName)
    {
        _queue.Enqueue(modelFileName);
        _signal.Release();
    }

    private string? ResolveBaseUrl()
    {
        var isLinuxPlatform = System.Runtime.InteropServices.RuntimeInformation.IsOSPlatform(System.Runtime.InteropServices.OSPlatform.Linux);
        var baseUrlKey = isLinuxPlatform ? "ThumbnailService:BaseUrlLinux" : "ThumbnailService:BaseUrl";

        var configured = _configuration[baseUrlKey];
        if (!string.IsNullOrWhiteSpace(configured))
            return configured.TrimEnd('/');

        var addresses = _server.Features.Get<IServerAddressesFeature>()?.Addresses;
        if (addresses == null || addresses.Count == 0)
            return null;

        var httpAddress = addresses
            .FirstOrDefault(a => a.StartsWith("http://", StringComparison.OrdinalIgnoreCase))
            ?? addresses.First();

        return NormalizeAddress(httpAddress).TrimEnd('/');
    }

    private static string NormalizeAddress(string address) =>
        address
            .Replace("://0.0.0.0:", "://127.0.0.1:", StringComparison.OrdinalIgnoreCase)
            .Replace("://+:", "://127.0.0.1:", StringComparison.OrdinalIgnoreCase)
            .Replace("://*:", "://127.0.0.1:", StringComparison.OrdinalIgnoreCase);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var isLinuxPlatform = System.Runtime.InteropServices.RuntimeInformation.IsOSPlatform(System.Runtime.InteropServices.OSPlatform.Linux);
        var browsersPathKey = isLinuxPlatform ? "ThumbnailService:BrowsersPathLinux" : "ThumbnailService:BrowsersPath";

        var browsersPath = _configuration[browsersPathKey];
        if (string.IsNullOrWhiteSpace(browsersPath))
        {
            _logger.LogError("{BrowsersPathKey} is not configured. Thumbnail generation is disabled.", browsersPathKey);
            return;
        }

        if (!Directory.Exists(browsersPath))
        {
            _logger.LogError("Playwright browsers folder not found at '{BrowsersPath}'.", browsersPath);
            return;
        }

        Environment.SetEnvironmentVariable("PLAYWRIGHT_BROWSERS_PATH", browsersPath);
        _logger.LogInformation("Playwright browsers path set to '{BrowsersPath}'.", browsersPath);

        try
        {
            await Task.Delay(Timeout.Infinite, _lifetime.ApplicationStarted);
        }
        catch (OperationCanceledException) when (_lifetime.ApplicationStarted.IsCancellationRequested)
        {
            // ApplicationStarted fired — expected, continue.
        }
        catch (OperationCanceledException)
        {
            return;
        }

        var baseUrl = ResolveBaseUrl();
        if (string.IsNullOrEmpty(baseUrl))
        {
            _logger.LogError("Could not resolve server base URL. Thumbnail generation is disabled.");
            return;
        }

        _logger.LogInformation("ThumbnailBackgroundService using base URL: {BaseUrl}", baseUrl);

        using var playwright = await Playwright.CreateAsync();
        await using var browser = await playwright.Chromium.LaunchAsync(new BrowserTypeLaunchOptions
        {
            Headless = true,
            Args = ["--no-sandbox", "--disable-setuid-sandbox"]
        });

        while (!stoppingToken.IsCancellationRequested)
        {
            await _signal.WaitAsync(stoppingToken);

            if (!_queue.TryDequeue(out var modelFileName))
                continue;

            await GenerateThumbnailAsync(browser, baseUrl, modelFileName, stoppingToken);
        }
    }

    private async Task GenerateThumbnailAsync(IBrowser browser, string baseUrl, string modelFileName, CancellationToken stoppingToken)
    {
        var isLinuxPlatform = System.Runtime.InteropServices.RuntimeInformation.IsOSPlatform(System.Runtime.InteropServices.OSPlatform.Linux);
        var FileStorage = isLinuxPlatform ? "FileStorageLinux" : "FileStorage";

        var thumbnailsDir = _configuration[$"{FileStorage}:ThumbnailsDir"];
        if (string.IsNullOrEmpty(thumbnailsDir))
        {
            _logger.LogError("Thumbnails path is not configured.");
            return;
        }

        var outputFileName = Path.GetFileNameWithoutExtension(modelFileName) + ".jpg";
        var outputPath = Path.Combine(thumbnailsDir, outputFileName);

        try
        {
            _logger.LogInformation("Generating thumbnail for {ModelFileName}", modelFileName);

            var context = await browser.NewContextAsync(new BrowserNewContextOptions
            {
                IgnoreHTTPSErrors = true,
                ViewportSize = new ViewportSize { Width = 1280, Height = 720 }
            });

            var page = await context.NewPageAsync();

            var url = $"{baseUrl}/ThumbnailGenerator?modelId={Uri.EscapeDataString(modelFileName)}";

            try
            {
                await page.GotoAsync(url, new PageGotoOptions
                {
                    WaitUntil = WaitUntilState.DOMContentLoaded,
                    Timeout = 600_000
                });
            }
            catch (PlaywrightException ex)
            {
                _logger.LogError(ex, "Failed to load thumbnail page for {ModelFileName}", modelFileName);
                await context.CloseAsync();
                return;
            }

            var readyState = await page.WaitForFunctionAsync(
                "() => window.__thumbnailReady === true || window.__thumbnailReady === 'error'",
                null,
                new PageWaitForFunctionOptions { Timeout = 600_000, PollingInterval = 500 });

            var value = await readyState.JsonValueAsync<object>();
            if (value is string s && s == "error")
            {
                _logger.LogError("Viewer reported a render error for {ModelFileName}. Thumbnail skipped.", modelFileName);
                await context.CloseAsync();
                return;
            }

            // Clip area
            Clip clip = new Clip { X = 0, Y = 0, Width = 1280, Height = 720 };
            var frameElement = await page.QuerySelectorAsync("#thumb-frame");
            if (frameElement != null)
            {
                var box = await frameElement.BoundingBoxAsync();
                if (box != null)
                    clip = new Clip { X = box.X, Y = box.Y, Width = box.Width, Height = box.Height };
            }

            var screenshotOptions = new PageScreenshotOptions
            {
                Path = outputPath,
                Type = ScreenshotType.Jpeg,
                Quality = 85,
                Clip = clip
            };

            await page.ScreenshotAsync(screenshotOptions);

            _logger.LogInformation("Thumbnail saved to {OutputPath}", outputPath);
            await context.CloseAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to generate thumbnail for {ModelFileName}", modelFileName);
        }
    }
}