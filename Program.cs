using PollingJobToken.AppModels;
using PollingJobToken.AppServices;
using PollingJobToken.Services;

using Scalar.AspNetCore;
using System.Text.Json.Serialization;

namespace PollingJobToken;

public class Program
{
    public static void Main(string[] args)
    {
        var builder = WebApplication.CreateBuilder(args);

        // Configure single-line console logging
        builder.Logging.ClearProviders();
        builder.Logging.AddSimpleConsole(options =>
        {
            options.SingleLine = true;
            options.TimestampFormat = "yyyy-MM-ddTHH:mm:ss.fffZ ";
            options.UseUtcTimestamp = true;
        });

        // Add services to the container.
        builder.Services
            .AddControllers()
            .AddJsonOptions(options =>
            {
                // Serialize enums as strings globally
                options.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter());
            });
        builder.Services.AddMemoryCache();

        // Use desired JobStore implementation - only one should be registered
        //builder.Services.AddSingleton<IJobStore, ConcurrentDictionaryJobStore>();
        builder.Services.AddSingleton<IJobStore, MemoryCacheJobStore>();

        // Register processors
        builder.Services.AddSingleton<IJobProcessor<WeatherForecastRequest, WeatherForecastResponse>, WeatherForecastJobProcessor>();
        builder.Services.AddSingleton<IJobProcessor<LotteryNumberRequest, LotteryNumberResponse>, LotteryNumberJobProcessor>();

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
