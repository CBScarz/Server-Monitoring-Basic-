using IMISMonitor.Hubs;
using IMISMonitor.Models;
using IMISMonitor.Services;

var builder = WebApplication.CreateBuilder(args);



builder.Services.Configure<MonitorSettings>(
    builder.Configuration.GetSection("MonitorSettings"));


builder.Services.AddSignalR();


builder.Services.AddControllers();
builder.Services.AddRazorPages();


builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new()
    {
        Title = "IMIS Monitor API",
        Version = "v1",
        Description = "REST API for the IMIS IP Status Monitor. Provides device status and downtime event history."
    });
});


builder.Services.AddHostedService<PingMonitorService>();


builder.Services.AddCors(options =>
{
    options.AddPolicy("LocalDev", policy =>
    {
        policy
            .WithOrigins(
                "http://localhost:5000")
            .AllowAnyHeader()
            .AllowAnyMethod()
            .AllowCredentials();
    });

    options.AddPolicy("Production", policy =>
    {
        policy
            .WithOrigins("https://your-internal-domain.local")
            .WithMethods("GET", "POST")
            .WithHeaders("Content-Type")
            .AllowCredentials();
    });
});


var app = builder.Build();


if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI(c =>
        c.SwaggerEndpoint("/swagger/v1/swagger.json", "IMIS Monitor API v1"));
    
   
    app.Use(async (context, next) =>
    {
        context.Response.Headers.Append("Content-Security-Policy", "default-src 'self'; script-src 'self' 'unsafe-inline'; style-src 'self' 'unsafe-inline'; img-src 'self' data:; font-src 'self'; connect-src 'self' ws: wss:; object-src 'none'; base-uri 'self'; frame-ancestors 'none';");
        await next();
    });
}

if (!app.Environment.IsDevelopment())
{
    app.Use(async (context, next) =>
    {
        context.Response.Headers.Append("X-Content-Type-Options", "nosniff");
        context.Response.Headers.Append("X-Frame-Options", "DENY");
        context.Response.Headers.Append("X-XSS-Protection", "1; mode=block");
        context.Response.Headers.Append("Referrer-Policy", "strict-origin-when-cross-origin");
        context.Response.Headers.Append("Content-Security-Policy", "default-src 'self'; script-src 'self' 'unsafe-inline'; style-src 'self' 'unsafe-inline'; img-src 'self' data:; font-src 'self'; connect-src 'self' ws: wss:; object-src 'none'; base-uri 'self'; frame-ancestors 'none';");
        context.Response.Headers.Append("Permissions-Policy", "camera=(), microphone=(), geolocation=(), fullscreen=(), payment=(), usb=()");
        await next();
    });
}

app.UseStaticFiles();
app.UseRouting();
app.UseCors(app.Environment.IsDevelopment() ? "LocalDev" : "Production");


app.MapHub<StatusHub>("/hubs/status");
app.MapControllers();
app.MapRazorPages();

app.Run();
