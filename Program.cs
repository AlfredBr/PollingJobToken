using api.Models;
using api.Services;
using Scalar.AspNetCore;

namespace api;

public class Program
{
    public static void Main(string[] args)
    {
        var builder = WebApplication.CreateBuilder(args);

        // Add services to the container.
        builder.Services.AddControllers();
        builder.Services.AddMemoryCache();

        // Swap => builder.Services.AddSingleton<IJobStore, InMemoryJobStore>();
        builder.Services.AddSingleton<IJobStore, CachedJobStore>();

        // Register processors
        builder.Services.AddSingleton<IJobProcessor<WeatherForecastRequest, WeatherForecastResponse>, WeatherForecastJobProcessor>();

        // Learn more about configuring OpenAPI at https://aka.ms/aspnet/openapi
        builder.Services.AddOpenApi();

        var app = builder.Build();

        // Configure the HTTP request pipeline.
        if (app.Environment.IsDevelopment())
        {
            app.MapOpenApi();
            app.MapScalarApiReference();
        }

        app.UseHttpsRedirection();
        app.UseAuthorization();
        app.MapControllers();
        app.Run();
    }
}
