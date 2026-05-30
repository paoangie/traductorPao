using Api_TutorIdiomas.Data;
using Api_TutorIdiomas.Filters;
using Api_TutorIdiomas.Middleware;
using Api_TutorIdiomas.Repositories;
using Api_TutorIdiomas.Services;
using Api_TutorIdiomas.Settings;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using System.Text;

var builder = WebApplication.CreateBuilder(args);

// ========================================================
// 1. CONFIGURACIÓN DE BASE DE DATOS (PostgreSQL)
// ========================================================
var connectionString = builder.Configuration.GetConnectionString("conexion");

builder.Services.AddDbContext<BdContext>(options =>
    options.UseNpgsql(connectionString));

// ========================================================
// 2. CONFIGURACIÓN DE JWT
// ========================================================
var jwtSettings = new JwtSettings();
builder.Configuration.GetSection("Jwt").Bind(jwtSettings);

builder.Services.AddSingleton(jwtSettings);

var key = Encoding.ASCII.GetBytes(jwtSettings.Key);

builder.Services.AddAuthentication(options =>
{
    options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
    options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
})
.AddJwtBearer(options =>
{
    options.TokenValidationParameters = new TokenValidationParameters
    {
        ValidateIssuerSigningKey = true,
        IssuerSigningKey = new SymmetricSecurityKey(key),

        ValidateIssuer = true,
        ValidIssuer = jwtSettings.Issuer,

        ValidateAudience = true,
        ValidAudience = jwtSettings.Audience,

        ValidateLifetime = true,
        ClockSkew = TimeSpan.Zero
    };
});

builder.Services.AddAuthorization();

// ========================================================
// 3. CONFIGURACIÓN DE CORS
// ========================================================
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowReactApp", policy =>
    {
        policy.WithOrigins(
                "http://localhost:3000",
                "http://localhost:5173",
                "http://localhost:4200"
            )
            .AllowAnyMethod()
            .AllowAnyHeader()
            .AllowCredentials();
    });
});

// ========================================================
// 4. CONFIGURACIÓN DE SETTINGS
// ========================================================
builder.Services.Configure<JwtSettings>(
    builder.Configuration.GetSection("Jwt")
);

builder.Services.Configure<GroqSettings>(
    builder.Configuration.GetSection("Groq")
);

// ========================================================
// 5. REGISTRO DE REPOSITORIOS
// ========================================================
builder.Services.AddScoped<IUserRepository, UserRepository>();
builder.Services.AddScoped<IRefreshTokenRepository, RefreshTokenRepository>();
builder.Services.AddScoped<ILessonRepository, LessonRepository>();
builder.Services.AddScoped<IProgressRepository, ProgressRepository>();
builder.Services.AddScoped<IPronunciationRepository, PronunciationRepository>();
builder.Services.AddScoped<ILanguageRepository, LanguageRepository>();
builder.Services.AddScoped<IExerciseRepository, ExerciseRepository>();
builder.Services.AddScoped<IMistakeRepository, MistakeRepository>();

// ========================================================
// 6. REGISTRO DE SERVICIOS
// ========================================================
builder.Services.AddScoped<IAuthService, AuthService>();
builder.Services.AddScoped<TokenService>();
builder.Services.AddScoped<GroqAiService>();
builder.Services.AddScoped<ProgressService>();
builder.Services.AddScoped<PronunciationService>();

// Refactor aplicado: el controlador depende de la interfaz,
// no directamente de la clase concreta.
builder.Services.AddScoped<IExerciseScoringService, ExerciseScoringService>();

builder.Services.AddScoped<DynamicExerciseService>();
builder.Services.AddScoped<TheoryService>();

// ========================================================
// 7. HTTP CLIENT PARA GROQ API
// ========================================================
builder.Services.AddHttpClient<GroqAiService>();

// ========================================================
// 8. CONTROLLERS
// ========================================================
builder.Services.AddControllers(options =>
{
    options.Filters.Add<ValidationFilter>();
})
.AddJsonOptions(options =>
{
    options.JsonSerializerOptions.ReferenceHandler =
        System.Text.Json.Serialization.ReferenceHandler.IgnoreCycles;
});

builder.Services.AddEndpointsApiExplorer();

// ========================================================
// 9. SWAGGER CON SOPORTE JWT
// ========================================================
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new OpenApiInfo
    {
        Title = "Tutor de Idiomas API",
        Version = "v1",
        Description = "API para plataforma adaptativa de aprendizaje de idiomas con IA",
        Contact = new OpenApiContact
        {
            Name = "PaoLingua",
            Email = "contacto@paolingua.com"
        }
    });

    c.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        In = ParameterLocation.Header,
        Description = "Ingrese el token JWT. Ejemplo: Bearer {token}",
        Name = "Authorization",
        Type = SecuritySchemeType.Http,
        BearerFormat = "JWT",
        Scheme = "bearer"
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
            Array.Empty<string>()
        }
    });
});

// ========================================================
// 10. CONSTRUIR LA APLICACIÓN
// ========================================================
var app = builder.Build();

// ========================================================
// 11. PIPELINE HTTP
// ========================================================
app.UseMiddleware<ExceptionMiddleware>();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();

app.UseCors("AllowReactApp");

app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

// ========================================================
// 12. APLICAR MIGRACIONES AUTOMÁTICAMENTE
// ========================================================
using (var scope = app.Services.CreateScope())
{
    var services = scope.ServiceProvider;

    try
    {
        var context = services.GetRequiredService<BdContext>();

        if ((await context.Database.GetPendingMigrationsAsync()).Any())
        {
            await context.Database.MigrateAsync();
            Console.WriteLine("Base de datos actualizada correctamente.");
        }
        else
        {
            Console.WriteLine("Base de datos está actualizada.");
        }
    }
    catch (Exception ex)
    {
        Console.WriteLine($"Error al migrar la base de datos: {ex.Message}");
    }
}

// ========================================================
// 13. EJECUTAR APLICACIÓN
// ========================================================
app.Run();