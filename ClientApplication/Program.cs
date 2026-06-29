using Europeana3D.Web.Services;
using Amazon.S3; // [ADDED for S3 integration]
using Amazon.Extensions.NETCore.Setup; // [ADDED for S3 integration]
using Amazon.Runtime;            // [ADDED for S3 integration]
using Europeana3D.Web.Formatter;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddControllersWithViews();


// Config

builder.Services.AddHttpClient();
builder.Services.AddCors();

// App Services
builder.Services.AddSingleton<IConfiguration>(builder.Configuration);
builder.Services.AddSingleton<XmlTemplateService>();
builder.Services.AddScoped<EuropeanaService>();
builder.Services.AddScoped<ViewerService>();
builder.Services.AddScoped<BridgeService>();

// [ADDED for S3 integration]
var awsOptions = new AWSOptions
{
    Region = Amazon.RegionEndpoint.EUWest1,
    Credentials = new BasicAWSCredentials(
        builder.Configuration["AWS:AccessKey"],
        builder.Configuration["AWS:SecretKey"])
};
builder.Services.AddDefaultAWSOptions(awsOptions);
builder.Services.AddAWSService<IAmazonS3>();
builder.Services.AddScoped<S3Service>();
builder.Services.AddScoped<ZenodoService>();
builder.Services.AddScoped<Europeana3D.Web.Services.RepositoryService>();
builder.Services.AddScoped<Europeana3D.Web.Services.ZenodoUploadService>();
builder.Services.AddScoped<Europeana3D.Web.Services.S3UploadService>();
builder.Services.AddControllers(
    options => options.InputFormatters.Add(new PlainTextFormatter())
    );

var app = builder.Build();

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();

app.UseRouting();
app.UseAuthentication();
app.UseAuthorization();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");

app.Run();