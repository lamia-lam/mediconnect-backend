using System.Text;
using MediatR;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using MedConnect.Data;
using MedConnect.Features.Users.Commands;
using MedConnect.Repositories;
using MedConnect.Services;
using Serilog;
using System.Security.Cryptography;
using StackExchange.Redis;
using Microsoft.Extensions.DependencyInjection;

// Configure Serilog from appsettings.json
var builder = WebApplication.CreateBuilder(args);

// Set up logging provider based on configuration
var loggingProvider = builder.Configuration["Logging:Provider"] ?? "Console";
if (loggingProvider == "Serilog")
{
    Log.Logger = new LoggerConfiguration()
        .ReadFrom.Configuration(builder.Configuration)
        .CreateLogger();
    builder.Host.UseSerilog();
}
// else use default logging (Console/File) via appsettings.json

// Add services to the container.
// builder.Services.AddSingleton(new CircuitBreakerService(3, TimeSpan.FromSeconds(30)));
builder.Services.AddSingleton<ICircuitBreakerService, CircuitBreakerService>();
builder.Services.AddControllers();

// Add health checks for the database
builder.Services.AddHealthChecks()
    .AddDbContextCheck<ApplicationDbContext>("Database");

// Configure Swagger
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new OpenApiInfo { Title = "MedConnect API", Version = "v1" });
    
    // Configure Swagger to use JWT Authentication
    c.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Description = "JWT Authorization header using the Bearer scheme. Example: 'Bearer {token}'",
        Name = "Authorization",
        In = ParameterLocation.Header,
        Type = SecuritySchemeType.ApiKey,
        Scheme = "Bearer"
    });
    
    c.AddSecurityRequirement(new OpenApiSecurityRequirement
    {
        {
            new OpenApiSecurityScheme
            {
                Reference = new OpenApiReference
                {
                    Type = ReferenceType.SecurityScheme,
                    Id = "Bearer"
                }
            },
            new string[] {}
        }
    });
});

var env = builder.Environment;
var configuration = builder.Configuration;

// Register JWT Service and Token Validation Cache
builder.Services.AddSingleton<JwtService>();


// Configure Database and Cache
if (env.IsDevelopment())
{
    builder.Services.AddSingleton<ITokenValidationCache, InMemoryTokenValidationCache>();
    builder.Services.AddSingleton<IUserRepository, InMemoryUserRepository>();
    builder.Services.AddSingleton<ICache, InMemoryCache>();

    builder.Services.AddDbContext<ApplicationDbContext>(options =>
        options.UseSqlite(configuration.GetConnectionString("DefaultConnection"))
    );

    //builder.Services.AddSingleton<IConnectionMultiplexer>(sp =>
    //    StackExchange.Redis.ConnectionMultiplexer.Connect(configuration.GetConnectionString("Redis") ?? "localhost")
    //);

}
else
{
    builder.Services.AddSingleton<ITokenValidationCache, TokenValidationCache>();
    builder.Services.AddScoped<IUserRepository, UserRepository>();

    builder.Services.AddDbContext<ApplicationDbContext>(options =>
        options.UseNpgsql(configuration.GetConnectionString("DefaultConnection"))
    );
    builder.Services.AddSingleton<IConnectionMultiplexer>(sp =>
        StackExchange.Redis.ConnectionMultiplexer.Connect(configuration.GetConnectionString("Redis") ?? "localhost")
    );
    builder.Services.AddSingleton<ICache, RedisCache>();
}

// Register Repositories
builder.Services.AddScoped(typeof(IRepository<>), typeof(Repository<>));


// Register MediatR
builder.Services.AddMediatR(cfg => cfg.RegisterServicesFromAssembly(typeof(Program).Assembly));

// Configure JWT Authentication
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
.AddJwtBearer(options =>
{
    var publicKeyPem = builder.Configuration["Jwt:PublicKeyPem"];

    if (string.IsNullOrWhiteSpace(publicKeyPem))
        throw new InvalidOperationException("JWT PublicKeyPem is not configured");
    var rsa = RSA.Create();
    rsa.ImportFromPem(publicKeyPem.ToCharArray());
    options.TokenValidationParameters = new TokenValidationParameters
    {
        ValidateIssuer = true,
        ValidateAudience = true,
        ValidateLifetime = true,
        ValidateIssuerSigningKey = true,
        ValidIssuer = builder.Configuration["Jwt:Issuer"],
        ValidAudience = builder.Configuration["Jwt:Audience"],
        IssuerSigningKey = new RsaSecurityKey(rsa),
        RequireSignedTokens = true,
        RequireExpirationTime = true,
        ValidAlgorithms = new[] { SecurityAlgorithms.RsaSha256 }
    };
    // JWT revocation/replay protection: check JTI blacklist
    options.Events = new JwtBearerEvents
    {
        OnTokenValidated = async context =>
        {
            var tokenValidationCache = context.HttpContext.RequestServices.GetRequiredService<ITokenValidationCache>();
            var principal = context.Principal;
            if (principal is null)
            {
                context.Fail("Invalid token principal.");
                return;
            }
            var username = principal.Identity?.Name;
            var jti = principal.Claims.FirstOrDefault(c => c.Type == System.IdentityModel.Tokens.Jwt.JwtRegisteredClaimNames.Jti)?.Value;
            if (!string.IsNullOrEmpty(username) && !string.IsNullOrEmpty(jti))
            {
                if (await tokenValidationCache.IsJtiRevokedAsync(username, jti))
                {
                    context.Fail("Token has been revoked.");
                    return;
                }
            }
        }
    };
});

builder.Services.AddAuthorization(options =>
{
    options.AddPolicy("RequireAdminRole", policy => policy.RequireRole("Admin"));
    options.AddPolicy("RequireDoctorOrPharma", policy => policy.RequireRole("Doctor", "Pharma"));
    options.AddPolicy("RequireEmailVerified", policy => policy.RequireClaim("email_verified", "true"));
});

// Add CORS
builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(policy =>
    {
        policy.AllowAnyOrigin()
              .AllowAnyHeader()
              .AllowAnyMethod();
    });
});

// Add anti-forgery
builder.Services.AddAntiforgery(options =>
{
    options.HeaderName = "X-XSRF-TOKEN";
});

var app = builder.Build();

// Global exception handler
app.UseExceptionHandler(errorApp =>
{
    errorApp.Run(async context =>
    {
        context.Response.StatusCode = 500;
        context.Response.ContentType = "application/json";
        var error = context.Features.Get<Microsoft.AspNetCore.Diagnostics.IExceptionHandlerFeature>();
        if (error != null)
        {
            var result = System.Text.Json.JsonSerializer.Serialize(new { message = "An unexpected error occurred.", detail = error.Error.Message });
            await context.Response.WriteAsync(result);
        }
    });
});

// Configure the HTTP request pipeline
app.UseSwagger();
app.UseSwaggerUI(c => {
    c.SwaggerEndpoint("/swagger/v1/swagger.json", "MedConnect API v1");
    //c.RoutePrefix = string.Empty; // Set Swagger as the default route
    c.RoutePrefix = "swagger"; // Set Swagger as the default route
});

app.UseHttpsRedirection();

// Use CORS
app.UseCors();

// Use CSP (Content Security Policy)
app.Use(async (context, next) =>
{
    context.Response.Headers.Append("Content-Security-Policy", "default-src 'self'; script-src 'self'; object-src 'none';");
    await next();
});

// Initialize the in-memory database
//var dbContext = app.Services.GetRequiredService<ApplicationDbContext>();
// Remove direct call to dbContext.Database.OpenConnection() and dbContext.Database.EnsureCreated() for SQLite in-memory, as this should be handled in a scoped context or via migration/seed logic.

app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

// Map health check endpoint
app.MapHealthChecks("/health");

app.Run();
