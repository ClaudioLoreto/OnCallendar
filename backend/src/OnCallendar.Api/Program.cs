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
using OnCallendar.Infrastructure.Notifications;
using OnCallendar.Infrastructure.Persistence;
using OnCallendar.Infrastructure.Persistence.Seed;

var builder = WebApplication.CreateBuilder(args);

// Override locale (gitignored) per dev: lo script start-expo.ps1 lo aggiorna
// con i tunnel cloudflared correnti. Non viene caricato in produzione perché
// il file non esiste lì.
builder.Configuration.AddJsonFile("appsettings.Local.json", optional: true, reloadOnChange: true);

// In container la cartella wwwroot viene aggiunta DOPO il `dotnet publish`
// (vedi Dockerfile: COPY --from=web /web/dist ./wwwroot/). Per assicurarsi
// che ASP.NET la trovi al boot, dichiariamo esplicitamente il WebRoot.
{
    var contentRoot = builder.Environment.ContentRootPath;
    var wwwrootDir = Path.Combine(contentRoot, "wwwroot");
    Directory.CreateDirectory(wwwrootDir);
    builder.Environment.WebRootPath = wwwrootDir;
}

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
// CORS: permissivo in dev, restrittivo in prod.
const string CorsPolicy = "OnCallendar.Cors";
builder.Services.AddCors(o => o.AddPolicy(CorsPolicy, p =>
{
    if (builder.Environment.IsDevelopment())
    {
        p.SetIsOriginAllowed(_ => true)
         .AllowAnyHeader()
         .AllowAnyMethod()
         .AllowCredentials();
    }
    else
    {
        p.WithOrigins(
                "https://api-production-e42a.up.railway.app",
                "https://oncallendar.app")
         .AllowAnyHeader()
         .AllowAnyMethod()
         .AllowCredentials();
    }
}));

// Default = postgres (sia in dev via Docker che in prod su Railway).
// Per uso locale con SqlServer LocalDB impostare DB_PROVIDER=sqlserver.
var dbProvider = (Environment.GetEnvironmentVariable("DB_PROVIDER")
                  ?? builder.Configuration["Database:Provider"]
                  ?? "postgres").Trim().ToLowerInvariant();
DatabaseProviderHelper.Override(dbProvider);

builder.Services.AddDbContext<ApplicationDbContext>(opt =>
{
    if (dbProvider is "postgres" or "postgresql")
    {
        // Railway inietta DATABASE_URL come postgres://user:pass@host:port/db.
        // In dev usiamo la ConnectionString "DefaultConnection" (formato Npgsql nativo).
        var raw = Environment.GetEnvironmentVariable("DATABASE_URL")
                  ?? builder.Configuration.GetConnectionString("DefaultConnection")
                  ?? throw new InvalidOperationException("DATABASE_URL/DefaultConnection non impostato");
        var conn = DbConnectionStringHelper.NormalizeNpgsql(raw);
        opt.UseNpgsql(conn, npg => npg.MigrationsAssembly("OnCallendar.Infrastructure"));
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
if (!builder.Environment.IsDevelopment()
    && jwtSettings.SecretKey.Contains("dev-only", StringComparison.OrdinalIgnoreCase))
{
    // WARNING: la chiave JWT è quella di default. In prod DEVI impostare
    // la variabile d'ambiente Jwt__SecretKey con un valore sicuro.
    Console.Error.WriteLine(
        "⚠️  WARNING: Jwt:SecretKey is the default dev value! " +
        "Set Jwt__SecretKey env var in production for security.");
}
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

// Mail (Resend via API HTTP, oppure SMTP via MailKit). Sezione "Mail" in appsettings.
builder.Services.Configure<MailSettings>(builder.Configuration.GetSection("Mail"));
builder.Services.Configure<PushSettings>(builder.Configuration.GetSection("Push"));
builder.Services.AddHttpClient("resend");
builder.Services.AddHttpClient("expo-push");

var mailProvider = (builder.Configuration["Mail:Provider"] ?? "Resend").Trim();
if (string.Equals(mailProvider, "Smtp", StringComparison.OrdinalIgnoreCase))
{
    builder.Services.AddScoped<IEmailSender, MailKitEmailSender>();
}
else
{
    builder.Services.AddScoped<IEmailSender, ResendEmailSender>();
}

// Notifiche multi-canale (in-app + email + push)
builder.Services.AddSingleton<INotificationTemplateRenderer, NotificationTemplateRenderer>();
builder.Services.AddScoped<IExpoPushSender, ExpoPushSender>();
builder.Services.AddScoped<INotificationDispatcher, NotificationDispatcher>();

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

// Migrate + Seed
using (var scope = app.Services.CreateScope())
{
    var sp = scope.ServiceProvider;
    var db = sp.GetRequiredService<ApplicationDbContext>();
    var um = sp.GetRequiredService<UserManager<ApplicationUser>>();
    var rm = sp.GetRequiredService<RoleManager<IdentityRole<Guid>>>();
    try
    {
        await DbSeeder.SeedAsync(db, um, rm);
    }
    catch (Exception ex)
    {
        // Il seed NON deve MAI impedire l'avvio dell'app.
        // In prod i dati esistono già; un errore di seed è solo un warning.
        var logger = sp.GetRequiredService<ILoggerFactory>().CreateLogger("DbSeeder");
        logger.LogError(ex, "Seed failed (non-fatal). L'app continua comunque.");
    }
}

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

// Static files per avatar caricati dagli utenti + SPA Web (Expo export → wwwroot/)
var wwwroot = Path.Combine(app.Environment.ContentRootPath, "wwwroot");
Directory.CreateDirectory(Path.Combine(wwwroot, "uploads", "avatars"));
var spaProvider = new Microsoft.Extensions.FileProviders.PhysicalFileProvider(wwwroot);
app.UseDefaultFiles(new Microsoft.AspNetCore.Builder.DefaultFilesOptions
{
    FileProvider = spaProvider,
    DefaultFileNames = new List<string> { "index.html" },
});
app.UseStaticFiles(new StaticFileOptions
{
    FileProvider = spaProvider,
    ServeUnknownFileTypes = true,
});

app.UseCors(CorsPolicy);
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();

app.MapGet("/health", () => Results.Ok(new { status = "ok", time = DateTime.UtcNow }));

// SPA fallback: per qualsiasi rotta del client (non /api, /swagger, /uploads, /health)
// rimanda a index.html così il client routing funziona anche su refresh.
var indexHtml = Path.Combine(wwwroot, "index.html");
if (File.Exists(indexHtml))
{
    // Constraint :nonfile esclude path con estensione (es. .js, .css) lasciandoli
    // gestire dallo static-files middleware.
    app.MapFallback("{*path:nonfile}", async ctx =>
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
        ctx.Response.ContentType = "text/html";
        await ctx.Response.SendFileAsync(indexHtml);
    });
}

app.Run();
