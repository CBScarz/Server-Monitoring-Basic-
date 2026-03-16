using IMISMonitor.Data;
using IMISMonitor.Hubs;
using IMISMonitor.Models;
using IMISMonitor.Services;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

// ─── Configuration ────────────────────────────────────────────────────────────
builder.Services.Configure<MonitorSettings>(
    builder.Configuration.GetSection("MonitorSettings"));

// ─── Data Layer ───────────────────────────────────────────────────────────────
builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseSqlite(builder.Configuration.GetConnectionString("DefaultConnection")));

// ─── Real-time ────────────────────────────────────────────────────────────────
builder.Services.AddSignalR();

// ─── MVC / Razor Pages ────────────────────────────────────────────────────────
builder.Services.AddControllers();
builder.Services.AddRazorPages();

// ─── Swagger ──────────────────────────────────────────────────────────────────
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

// ─── Background Services ──────────────────────────────────────────────────────
builder.Services.AddHostedService<PingMonitorService>();

// ─── Authentication ──────────────────────────────────────────────────────────
// For production, use Windows Authentication (IIS) or implement a proper auth scheme.
// TO FIX: Uncomment the following in production and configure accordingly.
//
// builder.Services.AddAuthentication(NegotiateDefaults.AuthenticationScheme)
//     .AddNegotiate();
//
// builder.Services.AddAuthorization();

// ─── CORS (restricted for production) ──────────────────────────────────────
builder.Services.AddCors(options =>
{
    options.AddPolicy("LocalDev", policy =>
    {
        // SECURITY: In development only. For production, use specific origins and remove AllowAnyHeader/AllowAnyMethod.
        policy
            .WithOrigins(
                "http://localhost:5000",
                "https://localhost:5001",
                "http://localhost:3000")
            .AllowAnyHeader()
            .AllowAnyMethod()
            .AllowCredentials();
    });

    options.AddPolicy("Production", policy =>
    {
        // SECURITY: For production deployment, replace with your actual internal domain(s).
        policy
            .WithOrigins("https://your-internal-domain.local")
            .WithMethods("GET", "POST")
            .WithHeaders("Content-Type")
            .AllowCredentials();
    });
});

// ─────────────────────────────────────────────────────────────────────────────
var app = builder.Build();
// ─────────────────────────────────────────────────────────────────────────────

// Ensure the SQLite database and schema exist before serving any requests.
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    db.Database.Migrate();
}

// ─── Middleware Pipeline ──────────────────────────────────────────────────────
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI(c =>
        c.SwaggerEndpoint("/swagger/v1/swagger.json", "IMIS Monitor API v1"));
    
    // Apply CSP headers in development as well
    app.Use(async (context, next) =>
    {
        context.Response.Headers.Append("Content-Security-Policy", "default-src 'self'; script-src 'self' 'unsafe-inline'; style-src 'self' 'unsafe-inline'; img-src 'self' data:; font-src 'self'; connect-src 'self' ws: wss:; object-src 'none'; base-uri 'self'; frame-ancestors 'none';");
        await next();
    });
}

// ─── Security Headers (for production) ─────────────────────────────────────────
// SECURITY: These headers protect against various client-side attacks.
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

// ─── Endpoints ────────────────────────────────────────────────────────────────
app.MapHub<StatusHub>("/hubs/status");
app.MapControllers();
app.MapRazorPages();

app.Run();
