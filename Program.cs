using api.Services;

#if (EnableApiDocs)
using Scalar.AspNetCore;
#endif

namespace api;

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
        builder.Services.AddControllers();
        builder.Services.AddMemoryCache();

        // Use desired JobStore implementation - only one should be registered
        //builder.Services.AddSingleton<IJobStore, ConcurrentDictionaryJobStore>();
        builder.Services.AddSingleton<IJobStore, MemoryCacheJobStore>();

#if (IncludeSampleJobs)
        using api.AppModels;
        using api.AppServices;

        // Register processors
        builder.Services.AddSingleton<IJobProcessor<WeatherForecastRequest, WeatherForecastResponse>, WeatherForecastJobProcessor>();
        builder.Services.AddSingleton<IJobProcessor<LotteryNumberRequest, LotteryNumberResponse>, LotteryNumberJobProcessor>();
#endif

#if (EnableApiDocs)
        // Learn more about configuring OpenAPI at https://aka.ms/aspnet/openapi
        builder.Services.AddOpenApi();
#endif

        var app = builder.Build();

        // Configure the HTTP request pipeline.
        if (app.Environment.IsDevelopment())
        {
#if (EnableApiDocs)
            app.MapOpenApi();
            app.MapScalarApiReference();
#endif
        }

        app.UseHttpsRedirection();
        app.UseAuthorization();
        app.MapControllers();
        app.Run();
    }
}
