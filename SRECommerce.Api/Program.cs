using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using SRECommerce.Api.Auth;
using SRECommerce.Api.Security;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http.Features;
using SRECommerce.Infrastructure.Data;
using System.Text;

var builder = WebApplication.CreateBuilder(args);
var connectionString = BuildSqliteConnectionString(builder);

// ------------------ DB CONTEXT ------------------
builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseSqlite(connectionString));
var allowedOrigins = builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>() ?? [];
if (allowedOrigins.Any(origin => !Uri.TryCreate(origin, UriKind.Absolute, out var uri)
    || uri.Scheme is not ("http" or "https") || uri.AbsolutePath != "/"
    || !string.IsNullOrEmpty(uri.Query) || !string.IsNullOrEmpty(uri.Fragment)
    || !string.IsNullOrEmpty(uri.UserInfo)))
    throw new InvalidOperationException("Cors:AllowedOrigins must contain explicit HTTP(S) origins.");

builder.Services.AddCors(options =>
{
    options.AddPolicy("ConfiguredOrigins", policy =>
    {
        if (allowedOrigins.Length > 0)
            policy.WithOrigins(allowedOrigins).AllowAnyMethod().AllowAnyHeader();
    });
});


// ------------------ JWT SETUP ------------------
builder.Services.Configure<JwtOptions>(builder.Configuration.GetSection("Jwt"));

var jwtOptions = new JwtOptions();
builder.Configuration.GetSection("Jwt").Bind(jwtOptions);
jwtOptions.Validate();

var key = Encoding.UTF8.GetBytes(jwtOptions.Key);

builder.Services.AddAuthentication(options =>
{
    options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
    options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
})
.AddJwtBearer(options =>
{
    options.RequireHttpsMetadata = !builder.Environment.IsDevelopment();
    options.MapInboundClaims = false;

    options.TokenValidationParameters = new TokenValidationParameters
    {
        ValidateIssuer = true,
        ValidateAudience = true,
        ValidateIssuerSigningKey = true,
        ValidateLifetime = true,
        RequireExpirationTime = true,
        ClockSkew = TimeSpan.FromSeconds(30),
        ValidAlgorithms = [SecurityAlgorithms.HmacSha256],
        NameClaimType = System.Security.Claims.ClaimTypes.Name,
        RoleClaimType = System.Security.Claims.ClaimTypes.Role,
        ValidIssuer = jwtOptions.Issuer,
        ValidAudience = jwtOptions.Audience,
        IssuerSigningKey = new SymmetricSecurityKey(key)
    };
});

builder.Services.AddAuthorization(options =>
    options.AddPolicy(SecurityPolicies.ManageUsers, policy =>
        policy.RequireAuthenticatedUser().AddRequirements(new ManageUsersRequirement())));
builder.Services.AddScoped<IAuthorizationHandler, ManageUsersHandler>();
builder.Services.Configure<FormOptions>(options =>
    options.MultipartBodyLengthLimit = AdminUploadLimits.MaxRequestBytes);
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(o =>
{
    o.CustomSchemaIds(type => type.FullName?.Replace("+", "."));
    // JWT Bearer for Swagger UI
    o.SwaggerDoc("v1", new Microsoft.OpenApi.Models.OpenApiInfo { Title = "SRECommerce API", Version = "v1" });

    var securityScheme = new Microsoft.OpenApi.Models.OpenApiSecurityScheme
    {
        Name = "Authorization",
        Type = Microsoft.OpenApi.Models.SecuritySchemeType.Http,
        Scheme = "bearer",
        BearerFormat = "JWT",
        In = Microsoft.OpenApi.Models.ParameterLocation.Header,
        Description = "Enter 'Bearer {token}'",
        Reference = new Microsoft.OpenApi.Models.OpenApiReference
        {
            Type = Microsoft.OpenApi.Models.ReferenceType.SecurityScheme,
            Id = "Bearer"
        }
    };

    o.AddSecurityDefinition("Bearer", securityScheme);
    o.AddSecurityRequirement(new Microsoft.OpenApi.Models.OpenApiSecurityRequirement
    {
        { securityScheme, new string[] { } }
    });
});

var app = builder.Build();


// ------------------ ENABLE CORS BEFORE AUTH ------------------
app.Use(async (context, next) =>
{
    context.Response.Headers["X-Content-Type-Options"] = "nosniff";
    if (context.Request.Path.StartsWithSegments("/api/auth")
        || context.Request.Path.StartsWithSegments("/api/admin/users"))
        context.Response.Headers.CacheControl = "no-store";
    await next();
});
if (!app.Environment.IsDevelopment()) app.UseHttpsRedirection();
app.UseCors("ConfiguredOrigins");


// ------------------ SWAGGER ------------------
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseStaticFiles();

app.UseAuthentication();
app.UseAuthorization();


// ------------------ SEED DB ------------------
var initializeDatabase = app.Configuration.GetValue<bool>("Database:Initialize");
var seedDemoData = app.Configuration.GetValue<bool>("DemoData:Enabled");
if ((initializeDatabase || seedDemoData) && !app.Environment.IsDevelopment())
    throw new InvalidOperationException("Database initialization and demo seeding are allowed only in Development.");
if (initializeDatabase || seedDemoData)
{
    using var scope = app.Services.CreateScope();
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    if (initializeDatabase) await db.Database.EnsureCreatedAsync();
    if (seedDemoData)
    {
        var password = app.Configuration["DemoData:Password"];
        if (string.IsNullOrWhiteSpace(password) || password.Length < 12)
            throw new InvalidOperationException("DemoData:Password must be supplied via local secrets and contain at least 12 characters.");
        await DbSeeder.SeedAsync(db, password);
    }
}

app.MapControllers();
app.MapGet("/health/live", () => Results.Ok(new { status = "healthy" }));
app.MapGet("/health/ready", async (AppDbContext db, CancellationToken cancellationToken) =>
{
    try
    {
        await db.Users.AsNoTracking().Select(u => u.Id).Take(1).ToListAsync(cancellationToken);
        await db.Orders.AsNoTracking().Select(o => o.Id).Take(1).ToListAsync(cancellationToken);
        return Results.Ok(new { status = "ready" });
    }
    catch (Exception ex) when (ex is not OperationCanceledException)
    {
        return Results.StatusCode(StatusCodes.Status503ServiceUnavailable);
    }
});
app.Run();

static string BuildSqliteConnectionString(WebApplicationBuilder builder)
{
    var configured = builder.Configuration.GetConnectionString("DefaultConnection")
        ?? "Data Source=App_Data/srecommerce.db";
    var sqlite = new SqliteConnectionStringBuilder(configured);

    if (string.IsNullOrWhiteSpace(sqlite.DataSource))
    {
        sqlite.DataSource = "App_Data/srecommerce.db";
    }

    if (!Path.IsPathRooted(sqlite.DataSource))
    {
        sqlite.DataSource = Path.Combine(builder.Environment.ContentRootPath, sqlite.DataSource);
    }

    return sqlite.ToString();
}

public partial class Program;

