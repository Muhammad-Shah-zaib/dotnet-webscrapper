global using WebScrapperApi.Services;
global using WebScrapperApi.Models;
global using WebScrapperApi.Data;
global using Microsoft.Playwright;
global using WebScrapperApi.Configuration;
global using WebScrapperApi.utils;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddControllers();

// Register custom services
builder.Services.AddScoped<CaterChoiceScraperService>();
builder.Services.AddScoped<AdamsScraperService>();
builder.Services.AddScoped<UtilityService>();
builder.Services.AddScoped<MetroScraperService>();
builder.Services.AddScoped<LoggerService>();

// singleton Scrapper Lock So only one Scrapper runs at a time
builder.Services.AddSingleton<ScraperLockService> ();

// Register database context
builder.Services.AddScoped<ScraperDbContext>();

// Scraper credentials Config
builder.Services.Configure<ScraperCredentialsConfig>(
    builder.Configuration.GetSection("ScraperCredentials"));

// Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// CORS FOR ALL ORIGINS
builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(policy =>
    {
        policy.AllowAnyOrigin()
              .AllowAnyHeader()
              .AllowAnyMethod();
    });
});
var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();

app.UseCors();

app.UseAuthorization();

app.MapControllers();

app.Run();
