using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using OnCallendar.Api.Auth;
using OnCallendar.Api.Services;
using OnCallendar.Api;
using OnCallendar.Application.Common.Interfaces;
using OnCallendar.Application.Common.Services;
using OnCallendar.Domain.Entities;
using OnCallendar.Domain.Services;
using OnCallendar.Infrastructure.Mail;
using OnCallendar.Infrastructure.Persistence;
using OnCallendar.Infrastructure.Persistence.Seed;

var builder = WebApplication.CreateBuilder(args);

// -------------------------------------------------------------------------
// CRITICO PER TEST DA iPHONE (Expo Go) SULLA STESSA RETE Wi-Fi:
// In ascolto su TUTTE le interfacce di rete della macchina Windows.
// Così l'iPhone può raggiungere http://<IP-LAN-PC>:5000/...
// In produzione (Railway) bindiamo solo sulla PORT fornita.
// -------------------------------------------------------------------------
if (builder.Environment.IsDevelopment())
{
    builder.WebHost.UseUrls("http://0.0.0.0:5000", "https://0.0.0.0:5001");
}
else
{
    var port = Environment.GetEnvironmentVariable("PORT") ?? "8080";
    builder.WebHost.UseUrls($"http://0.0.0.0:{port}");
}

// -------------------------------------------------------------------------
// CORS per app mobile in development. In Expo Go, durante il dev,
// le richieste arrivano dall'IP del telefono e non hanno un Origin "browser",
// ma se in futuro userai Expo Web o un dashboard admin web ti serve.
// In dev usiamo policy permissiva; in prod stringeremo.
// -------------------------------------------------------------------------
const string DevCorsPolicy = "OnCallendar.Dev";
builder.Services.AddCors(o => o.AddPolicy(DevCorsPolicy, p =>
{
    p.SetIsOriginAllowed(_ => true)   // dev: qualsiasi origin (incluso null)
     .AllowAnyHeader()
     .AllowAnyMethod()
     .AllowCredentials();
}));

builder.Services.AddDbContext<ApplicationDbContext>(opt =>
{
    var provider = (Environment.GetEnvironmentVariable("DB_PROVIDER")
                    ?? builder.Configuration["Database:Provider"]
                    ?? "sqlserver").ToLowerInvariant();

    if (provider == "postgres" || provider == "postgresql")
    {
        // Railway iniettà DATABASE_URL nel formato postgres://user:pass@host:port/db.
        var raw = Environment.GetEnvironmentVariable("DATABASE_URL")
                  ?? builder.Configuration.GetConnectionString("DefaultConnection")
                  ?? throw new InvalidOperationException("DATABASE_URL non impostato");
        var conn = DbConnectionStringHelper.NormalizeNpgsql(raw);
        opt.UseNpgsql(conn);
    }
    else
    {
        opt.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection"));
    }
});

builder.Services.AddScoped<IApplicationDbContext>(sp =>
    sp.GetRequiredService<ApplicationDbContext>());

// HTTP-context aware services
builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<ITenantProvider, HttpContextTenantProvider>();
builder.Services.AddScoped<ICurrentUserService, HttpContextCurrentUserService>();
builder.Services.AddScoped<IAuditLogger, AuditLogger>();
// Identity
builder.Services
    .AddIdentity<ApplicationUser, IdentityRole<Guid>>(opt =>
    {
        opt.Password.RequireDigit = true;
        opt.Password.RequireUppercase = true;
        opt.Password.RequireNonAlphanumeric = true;
        opt.Password.RequiredLength = 8;
        opt.User.RequireUniqueEmail = true;
    })
    .AddEntityFrameworkStores<ApplicationDbContext>()
    .AddDefaultTokenProviders();

// JWT
var jwtSettings = builder.Configuration.GetSection("Jwt").Get<JwtSettings>() ?? new JwtSettings();
builder.Services.AddSingleton(jwtSettings);
builder.Services.AddSingleton<IJwtTokenService, JwtTokenService>();

builder.Services
    .AddAuthentication(o =>
    {
        o.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
        o.DefaultChallengeScheme    = JwtBearerDefaults.AuthenticationScheme;
    })
    .AddJwtBearer(o =>
    {
        o.RequireHttpsMetadata = false;
        o.SaveToken = true;
        o.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            ValidIssuer = jwtSettings.Issuer,
            ValidAudience = jwtSettings.Audience,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtSettings.SecretKey)),
            ClockSkew = TimeSpan.FromMinutes(2),
        };
    });
builder.Services.AddAuthorization();

// Domain services
builder.Services.AddScoped<IShiftValidationService, ShiftValidationService>();

// Mail (SMTP via MailKit). Sezione "Mail" in appsettings.
builder.Services.Configure<MailSettings>(builder.Configuration.GetSection("Mail"));
builder.Services.AddScoped<IEmailSender, MailKitEmailSender>();

builder.Services.AddControllers()
    .AddJsonOptions(opt =>
    {
        // Force all DateTime values to be serialized as UTC ISO 8601 with Z suffix.
        // Without this, EF Core returns DateTime with Kind=Unspecified and JS parses
        // them as local time, causing a timezone offset (e.g. "2 ore fa" sfalsato).
        opt.JsonSerializerOptions.Converters.Add(new UtcDateTimeConverter());
        opt.JsonSerializerOptions.Converters.Add(new UtcNullableDateTimeConverter());
    });
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new OpenApiInfo { Title = "OnCallendar API", Version = "v1" });
    var jwtScheme = new OpenApiSecurityScheme
    {
        Name = "Authorization",
        Description = "Inserisci: Bearer {token}",
        In = ParameterLocation.Header,
        Type = SecuritySchemeType.ApiKey,
        Scheme = "Bearer",
        Reference = new OpenApiReference { Id = "Bearer", Type = ReferenceType.SecurityScheme }
    };
    c.AddSecurityDefinition("Bearer", jwtScheme);
    c.AddSecurityRequirement(new OpenApiSecurityRequirement { [jwtScheme] = Array.Empty<string>() });
});

var app = builder.Build();

// Migrate + Seed (dev: crea tenant L'Aquila, admin di default, 3 medici)
using (var scope = app.Services.CreateScope())
{
    var sp = scope.ServiceProvider;
    var db = sp.GetRequiredService<ApplicationDbContext>();
    var um = sp.GetRequiredService<UserManager<ApplicationUser>>();
    var rm = sp.GetRequiredService<RoleManager<IdentityRole<Guid>>>();
    await DbSeeder.SeedAsync(db, um, rm);
}

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

// Static files per avatar caricati dagli utenti
var wwwroot = Path.Combine(app.Environment.ContentRootPath, "wwwroot");
Directory.CreateDirectory(Path.Combine(wwwroot, "uploads", "avatars"));
app.UseStaticFiles();

app.UseCors(DevCorsPolicy);
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();

app.MapGet("/health", () => Results.Ok(new { status = "ok", time = DateTime.UtcNow }));

// SPA Web app: serve i file statici esportati da Expo Web (wwwroot/app)
// e fa il fallback di tutte le rotte non /api, non /swagger e non /uploads
// su index.html, così il client routing funziona anche su refresh.
var spaRoot = Path.Combine(wwwroot, "app");
if (Directory.Exists(spaRoot))
{
    var indexHtml = Path.Combine(spaRoot, "index.html");
    app.MapFallback(async ctx =>
    {
        var path = ctx.Request.Path.Value ?? string.Empty;
        if (path.StartsWith("/api", StringComparison.OrdinalIgnoreCase) ||
            path.StartsWith("/swagger", StringComparison.OrdinalIgnoreCase) ||
            path.StartsWith("/uploads", StringComparison.OrdinalIgnoreCase) ||
            path.StartsWith("/health", StringComparison.OrdinalIgnoreCase))
        {
            ctx.Response.StatusCode = StatusCodes.Status404NotFound;
            return;
        }

        // Tenta di servire un file specifico richiesto (asset, _expo, ...)
        var requested = Path.Combine(spaRoot, path.TrimStart('/').Replace('/', Path.DirectorySeparatorChar));
        if (File.Exists(requested))
        {
            var ext = Path.GetExtension(requested).ToLowerInvariant();
            var mime = ext switch
            {
                ".js" => "application/javascript",
                ".css" => "text/css",
                ".html" => "text/html",
                ".json" => "application/json",
                ".png" => "image/png",
                ".jpg" or ".jpeg" => "image/jpeg",
                ".svg" => "image/svg+xml",
                ".ttf" => "font/ttf",
                ".woff" => "font/woff",
                ".woff2" => "font/woff2",
                ".ico" => "image/x-icon",
                _ => "application/octet-stream",
            };
            ctx.Response.ContentType = mime;
            await ctx.Response.SendFileAsync(requested);
            return;
        }

        // Fallback su index.html per il client routing
        ctx.Response.ContentType = "text/html";
        await ctx.Response.SendFileAsync(indexHtml);
    });
}

app.Run();
