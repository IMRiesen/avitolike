using AvitoLike.Data;
using AvitoLike.Models;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Npgsql;
using System.Text;

var builder = WebApplication.CreateBuilder(args);

// Добавление сервисов
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();

// Настройка базы данных
builder.Services.AddDbContext<AppDbContext>(options =>
{
    options.UseNpgsql(builder.Configuration.GetConnectionString("DefaultConnection"));
    options.LogTo(Console.WriteLine, LogLevel.Information);
});

// Настройка аутентификации JWT
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            ValidIssuer = builder.Configuration["Jwt:Issuer"],
            ValidAudience = builder.Configuration["Jwt:Audience"],
            IssuerSigningKey = new SymmetricSecurityKey(
                Encoding.UTF8.GetBytes(builder.Configuration["Jwt:Key"]))
        };
    });

// Настройка CORS
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowAll", policy =>
    {
        policy.AllowAnyOrigin()
              .AllowAnyMethod()
              .AllowAnyHeader();
    });
});

var app = builder.Build();

// Настройка прослушивания всех интерфейсов
app.Urls.Add("http://0.0.0.0:5000");

// Проверка подключения к БД при запуске
using (var scope = app.Services.CreateScope())
{
    var services = scope.ServiceProvider;
    var logger = services.GetRequiredService<ILogger<Program>>();

    try
    {
        var db = services.GetRequiredService<AppDbContext>();
        await db.Database.OpenConnectionAsync();
        logger.LogInformation("✅ Подключение к БД успешно установлено");

        if ((await db.Database.GetPendingMigrationsAsync()).Any())
        {
            logger.LogInformation("🔄 Применяем миграции...");
            await db.Database.MigrateAsync();
            logger.LogInformation("✅ Миграции успешно применены");
        }

        // Проверка наличия ролей
        if (!await db.Roles.AnyAsync())
        {
            logger.LogInformation("🔄 Добавляем роли...");
            db.Roles.AddRange(
                new Role { Name = "Admin" },
                new Role { Name = "User" },
                new Role { Name = "Moderator" }
            );
            await db.SaveChangesAsync();
        }
    }
    catch (Exception ex)
    {
        logger.LogCritical(ex, "❌ Ошибка при инициализации БД");
        throw;
    }
}

// Middleware pipeline
app.UseCors("AllowAll"); // Применяем CORS всегда

if (app.Environment.IsDevelopment())
{
    app.UseDeveloperExceptionPage();
}

// Важно: UseAuthentication до UseAuthorization
app.UseAuthentication();
app.UseAuthorization();

// Настройка обработки статических файлов
app.UseStaticFiles(new StaticFileOptions
{
    ServeUnknownFileTypes = true,
    DefaultContentType = "text/html"
});

// Перенаправление с корня на index.html
app.Use(async (context, next) =>
{
    if (context.Request.Path == "/")
    {
        context.Response.Redirect("/index.html");
        return;
    }
    await next();
});

// Маршрутизация API
app.MapControllers();

// Fallback на index.html для SPA
app.MapFallbackToFile("index.html");

app.Run();