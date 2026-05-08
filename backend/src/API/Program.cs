using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using SkillRegistry.Application.Services;
using SkillRegistry.Infrastructure.Persistence;
using SkillRegistry.Infrastructure.Services;

var builder = WebApplication.CreateBuilder(args);

builder.Services.Configure<FormOptions>(options =>
{
    options.MultipartBodyLengthLimit = 104_857_600;
});

builder.Services.AddHttpContextAccessor();
builder.Services.AddHttpClient();
builder.Services.AddControllers();
builder.Services.AddOpenApi();
builder.Services.AddApplication();
builder.Services.AddInfrastructure(builder.Configuration);

ConfigureJwt(builder);

var corsOrigins = builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>() ?? [];
if (corsOrigins.Length > 0)
{
    builder.Services.AddCors(options =>
    {
        options.AddDefaultPolicy(policy =>
            policy.WithOrigins(corsOrigins).AllowAnyHeader().AllowAnyMethod());
    });
}

var app = builder.Build();

if (app.Environment.IsDevelopment())
    app.MapOpenApi();

using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<SkillRegistryDbContext>();
    db.Database.Migrate();
}

app.UseHttpsRedirection();
app.UseRouting();
if (corsOrigins.Length > 0)
    app.UseCors();

app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

app.MapGet("/health", () => Results.Json(new { status = "ok" }));
app.MapGet("/api/health", () => Results.Json(new { status = "ok" }));

app.Run();

static void ConfigureJwt(WebApplicationBuilder builder)
{
    var jwtSecret = builder.Configuration["JWT:SecretKey"];
    if (string.IsNullOrWhiteSpace(jwtSecret))
    {
        if (!builder.Environment.IsDevelopment())
            throw new InvalidOperationException("JWT:SecretKey must be configured.");
        jwtSecret = "skill-registry-dev-secret-min-32-chars!!";
    }

    if (jwtSecret.Length < 32)
        throw new InvalidOperationException("JWT:SecretKey must be at least 32 characters.");

    var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtSecret));

    builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
        .AddJwtBearer(options =>
        {
            options.TokenValidationParameters = new TokenValidationParameters
            {
                ValidateIssuerSigningKey = true,
                IssuerSigningKey = key,
                ValidateIssuer = true,
                ValidIssuer = builder.Configuration["JWT:Issuer"] ?? "SkillRegistry",
                ValidateAudience = true,
                ValidAudience = builder.Configuration["JWT:Audience"] ?? "SkillRegistry",
                ValidateLifetime = true,
                ClockSkew = TimeSpan.FromMinutes(2),
            };
        });

    builder.Services.AddAuthorization(options =>
    {
        if (builder.Configuration.GetValue<bool>("Auth:RequireBearer"))
        {
            options.FallbackPolicy = new AuthorizationPolicyBuilder()
                .AddAuthenticationSchemes(JwtBearerDefaults.AuthenticationScheme)
                .RequireAuthenticatedUser()
                .Build();
        }
    });
}
