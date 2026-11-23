using System.Text;
using System.Text.Json.Serialization;
using System.Threading.RateLimiting;
using GovAsset.Api.Data;
using GovAsset.Api.Security;
using GovAsset.Api.Services;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Npgsql;

var builder = WebApplication.CreateBuilder(args);
var cfg = builder.Configuration;

// ------------------------------------------------------------------ logs (uma linha por evento, com hora e cor)
builder.Logging.ClearProviders();
builder.Logging.AddSimpleConsole(o =>
{
    o.SingleLine = true;
    o.TimestampFormat = "HH:mm:ss ";
    o.UseUtcTimestamp = false;
    o.ColorBehavior = Microsoft.Extensions.Logging.Console.LoggerColorBehavior.Enabled;
});
builder.Logging.AddFilter("Microsoft.AspNetCore", LogLevel.Warning);
builder.Logging.AddFilter("Microsoft.EntityFrameworkCore", LogLevel.Warning);
builder.Logging.AddFilter("Microsoft.Hosting.Lifetime", LogLevel.Warning);

// ------------------------------------------------------------------ banco (PostgreSQL)
builder.Services.AddDbContext<GovAssetDbContext>(o => o.UseNpgsql(ResolverConexao(cfg)));

// ------------------------------------------------------------------ serviços da aplicação
builder.Services.AddSingleton(TimeProvider.System);
builder.Services.AddSingleton<IEvidenceStorage, LocalEvidenceStorage>();
builder.Services.AddSingleton<TokenService>();
builder.Services.AddScoped<ConciliationService>();
builder.Services.AddScoped<CsvImportService>();

builder.Services.AddControllers()
    .AddJsonOptions(o => o.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter()));
builder.Services.AddProblemDetails();

// ------------------------------------------------------------------ autenticação JWT
var chaveJwt = cfg["Jwt:Key"];
if (string.IsNullOrWhiteSpace(chaveJwt) || chaveJwt.Length < 32)
    throw new InvalidOperationException("Configure Jwt:Key (variável Jwt__Key) com pelo menos 32 caracteres.");

builder.Services
    .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(o =>
    {
        o.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidIssuer = cfg["Jwt:Issuer"],
            ValidateAudience = true,
            ValidAudience = cfg["Jwt:Audience"],
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(chaveJwt)),
            ClockSkew = TimeSpan.FromMinutes(1),
        };
    });
builder.Services.AddAuthorization();

// ------------------------------------------------------------------ CORS (front na Vercel, API na Render)
var origens = cfg.GetSection("Cors:AllowedOrigins").Get<string[]>() ?? Array.Empty<string>();
var origensCsv = cfg["CORS_ORIGINS"]; // alternativa simples: lista separada por vírgula
if (!string.IsNullOrWhiteSpace(origensCsv))
    origens = origens.Concat(origensCsv.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)).ToArray();

builder.Services.AddCors(o => o.AddPolicy("frontend", p =>
    p.WithOrigins(origens.Select(x => x.TrimEnd('/')).ToArray())
     .AllowAnyHeader()
     .AllowAnyMethod()));

// ------------------------------------------------------------------ limite de requisições (anti-abuso)
builder.Services.AddRateLimiter(o =>
{
    o.RejectionStatusCode = StatusCodes.Status429TooManyRequests;

    static string Cliente(HttpContext c) => c.Connection.RemoteIpAddress?.ToString() ?? "desconhecido";

    o.AddPolicy("relatos", c => RateLimitPartition.GetFixedWindowLimiter(Cliente(c), _ => new FixedWindowRateLimiterOptions
    {
        PermitLimit = 10, Window = TimeSpan.FromMinutes(10), QueueLimit = 0,
    }));
    o.AddPolicy("login", c => RateLimitPartition.GetFixedWindowLimiter(Cliente(c), _ => new FixedWindowRateLimiterOptions
    {
        PermitLimit = 5, Window = TimeSpan.FromMinutes(1), QueueLimit = 0,
    }));
});

var app = builder.Build();

app.UseExceptionHandler();
var logHttp = app.Services.GetRequiredService<ILoggerFactory>().CreateLogger("HTTP");
app.Use(async (ctx, next) =>
{
    ctx.Response.Headers["X-Content-Type-Options"] = "nosniff";

    // log de cada requisição: método, rota (sem query string), status e tempo. Ignora health check e preflight.
    var caminho = ctx.Request.Path.Value ?? "/";
    if (caminho == "/api/saude" || HttpMethods.IsOptions(ctx.Request.Method)) { await next(); return; }

    var inicio = System.Diagnostics.Stopwatch.GetTimestamp();
    var falhou = false;
    try { await next(); }
    catch { falhou = true; throw; }   // a exceção segue para o UseExceptionHandler (que devolve 500)
    finally
    {
        var ms = System.Diagnostics.Stopwatch.GetElapsedTime(inicio).TotalMilliseconds;
        var status = falhou ? 500 : ctx.Response.StatusCode;
        var simbolo = status >= 500 ? "✖" : status >= 400 ? "⚠" : "✔";
        var nivel = status >= 500 ? LogLevel.Error : status >= 400 ? LogLevel.Warning : LogLevel.Information;
        logHttp.Log(nivel, "{Simbolo} {Metodo,-6} {Caminho} → {Status} ({Ms:0} ms)",
            simbolo, ctx.Request.Method, caminho, status, ms);
    }
});
app.UseCors("frontend");
app.UseRateLimiter();
app.UseAuthentication();
app.UseAuthorization();

app.MapGet("/api/saude", () => Results.Ok(new { status = "ok" }));
app.MapControllers();

var logInicio = app.Services.GetRequiredService<ILoggerFactory>().CreateLogger("GovAsset");
await DbSeeder.InicializarAsync(app.Services, cfg, logInicio);

app.Lifetime.ApplicationStarted.Register(() =>
{
    var porta = Environment.GetEnvironmentVariable("PORT") ?? "8080";
    logInicio.LogInformation("────────────────────────────────────────────────────");
    logInicio.LogInformation(" GovAsset API no ar  ·  ambiente {Ambiente}", app.Environment.EnvironmentName);
    logInicio.LogInformation(" Saúde ....... /api/saude   (porta interna {Porta})", porta);
    logInicio.LogInformation(" Local ....... API http://localhost:5080 · Front http://localhost:5173");
    logInicio.LogInformation("────────────────────────────────────────────────────");
});
app.Run();

// Aceita connection string do Npgsql ou DATABASE_URL no formato postgres://usuario:senha@host:porta/banco (Render).
static string ResolverConexao(IConfiguration cfg)
{
    var url = cfg["DATABASE_URL"];
    if (!string.IsNullOrWhiteSpace(url) && url.StartsWith("postgres", StringComparison.OrdinalIgnoreCase))
    {
        var uri = new Uri(url);
        var partes = uri.UserInfo.Split(':', 2);
        return new NpgsqlConnectionStringBuilder
        {
            Host = uri.Host,
            Port = uri.Port > 0 ? uri.Port : 5432,
            Database = uri.AbsolutePath.TrimStart('/'),
            Username = Uri.UnescapeDataString(partes[0]),
            Password = partes.Length > 1 ? Uri.UnescapeDataString(partes[1]) : null,
            SslMode = SslMode.Prefer,
        }.ConnectionString;
    }
    return cfg.GetConnectionString("Default")
           ?? throw new InvalidOperationException("Configure ConnectionStrings:Default ou DATABASE_URL.");
}
