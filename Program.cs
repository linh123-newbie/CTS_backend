using CTS_backend.Data;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using System.Text.Json;
using Amazon;
using Amazon.Runtime;
using Amazon.S3;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var connectionString =
    builder.Configuration.GetConnectionString("DefaultConnection")
    ?? throw new InvalidOperationException(
        "not found connection string DefaultConnection."
    );

builder.Services.AddSingleton<NpgsqlDataSource>(_ =>
{
    var dataSourceBuilder =
        new NpgsqlDataSourceBuilder(connectionString);

    dataSourceBuilder.EnableDynamicJson();

    // Lưu JSONB theo dạng x, y, kind thay vì X, Y, Kind
    dataSourceBuilder.ConfigureJsonOptions(
        new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            PropertyNameCaseInsensitive = true
        }
    );

    return dataSourceBuilder.Build();
});

builder.Services.AddDbContext<AppDbContext>(
    (serviceProvider, options) =>
    {
        var dataSource =
            serviceProvider.GetRequiredService<NpgsqlDataSource>();

        options.UseNpgsql(dataSource);
    }
);

builder.Services.AddHttpClient("UltrasoundAi", client =>
{
    client.BaseAddress = new Uri("https://ultrasound.dangkhoa3ln.com/");
});
builder.Services.AddHttpClient("WaveformAi", client =>
{
    client.BaseAddress = new Uri("https://waveform.dangkhoa3ln.com/");
});

builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowFrontend", policy =>
    {
        policy.WithOrigins("https://project.dangkhoa3ln.com", "http://localhost:5173")
              .AllowAnyHeader()
              .AllowAnyMethod();
    });
});

// builder.Services.AddSingleton<IAmazonS3>(sp =>
// {
//     var config = sp.GetRequiredService<IConfiguration>();

//     // var regionName = config["AWS:Region"];
//     // var accessKey = config["AWS:AWS_ACCESS_KEY_ID"];
//     // var secretKey = config["AWS:AWS_SECRET_ACCESS_KEY"];
//     // if (string.IsNullOrWhiteSpace(regionName))
//     //     throw new InvalidOperationException("Thiếu AWS:Region");

//     // if (string.IsNullOrWhiteSpace(accessKey))
//     //     throw new InvalidOperationException("Thiếu AWS:AWS_ACCESS_KEY_ID");

//     // if (string.IsNullOrWhiteSpace(secretKey))
//     //     throw new InvalidOperationException("Thiếu AWS:AWS_SECRET_ACCESS_KEY");

//     var region = RegionEndpoint.GetBySystemName(regionName);

//     if (!string.IsNullOrWhiteSpace(accessKey) &&
//         !string.IsNullOrWhiteSpace(secretKey))
//     {
//         var credentials = new BasicAWSCredentials(accessKey, secretKey);
//         return new AmazonS3Client(credentials, region);
//     }

//     return new AmazonS3Client(region);
// });


var app = builder.Build();

// Configure the HTTP request pipeline.
app.UseSwagger();
app.UseSwaggerUI();


app.UseHttpsRedirection();
app.UseCors("AllowFrontend");

app.UseAuthorization();

app.MapControllers();
app.Run();


