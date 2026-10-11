using System.Text.Json.Serialization;
using FluentValidation;
using GorevTakip.Data;
using GorevTakip.Endpoints;
using GorevTakip.Infrastructure;
using GorevTakip.Services;
using GorevTakip.Validators;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

// --- Veritabanı ---
builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseSqlite(builder.Configuration.GetConnectionString("Default")
                      ?? "Data Source=gorevtakip.db"));

// --- Servisler (iş mantığı katmanı) ---
// Scoped: her HTTP isteği için bir tane oluşturulur (DbContext ile aynı ömür)
builder.Services.AddScoped<IProjectService, ProjectService>();
builder.Services.AddScoped<ITaskService, TaskService>();

// --- Doğrulama: Validators klasöründeki tüm validator sınıflarını otomatik kaydeder ---
builder.Services.AddValidatorsFromAssemblyContaining<CreateProjectRequestValidator>();

// --- Global hata yönetimi ---
builder.Services.AddExceptionHandler<GlobalExceptionHandler>();
builder.Services.AddProblemDetails();

// --- CORS: Aşama 3'te React arayüzü farklı bir porttan API'ye istek atacak ---
builder.Services.AddCors(options =>
{
    options.AddPolicy("Frontend", policy => policy
        .WithOrigins("http://localhost:5173", "http://localhost:3000") // Vite ve Create React App varsayılan portları
        .AllowAnyHeader()
        .AllowAnyMethod());
});

// Enum değerleri JSON'da sayı yerine okunur metin olsun ("Todo", "Done" gibi)
builder.Services.ConfigureHttpJsonOptions(options =>
    options.SerializerOptions.Converters.Add(new JsonStringEnumConverter()));

// --- Swagger ---
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var app = builder.Build();

// Hata yakalayıcı pipeline'ın EN BAŞINDA olmalı ki sonraki her şeydeki hatayı yakalasın
app.UseExceptionHandler();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseCors("Frontend");

app.MapProjectEndpoints();
app.MapTaskEndpoints();

app.Run();
