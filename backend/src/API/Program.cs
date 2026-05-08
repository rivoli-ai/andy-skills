using Microsoft.AspNetCore.Http.Features;
using Microsoft.EntityFrameworkCore;
using SkillRegistry.Application.Services;
using SkillRegistry.Infrastructure.Persistence;
using SkillRegistry.Infrastructure.Services;

var builder = WebApplication.CreateBuilder(args);

builder.Services.Configure<FormOptions>(options =>
{
    options.MultipartBodyLengthLimit = 104_857_600;
});

builder.Services.AddHttpContextAccessor();
builder.Services.AddControllers();
builder.Services.AddOpenApi();
builder.Services.AddApplication();
builder.Services.AddInfrastructure(builder.Configuration);

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

app.MapControllers();

app.MapGet("/health", () => Results.Json(new { status = "ok" }));
app.MapGet("/api/health", () => Results.Json(new { status = "ok" }));

app.Run();
