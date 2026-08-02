using Common.SampleDataGenerator;
using Common.SampleDataGenerator.RAG.Abstractions;
using Common.SampleDataGenerator.RAG.Indexing;
using Common.SampleDataGenerator.RAG.Ingestion;
using Common.SampleDataGenerator.RAG.Models;
using Common.SampleDataGenerator.RAG.Retrievals;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.AI;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi;
using Serilog;
using System.Text;
using Ticketing.Api.Auth;
using Ticketing.Api.Endpoints;
using Ticketing.Api.Middlewares;
using Ticketing.Api.Setup;
using Ticketing.Application.Abstractions;
using Ticketing.Application.Abstractions.Auth;
using Ticketing.Application.Abstractions.Repositories;
using Ticketing.Infrastructure.Auth;
using Ticketing.Infrastructure.Persistence;
using Ticketing.Infrastructure.Persistence.Outbox;
using Ticketing.Infrastructure.Repositories;

Log.Logger = new LoggerConfiguration()
    .MinimumLevel.Information()
    .MinimumLevel.Override("Microsoft", Serilog.Events.LogEventLevel.Warning)
    .MinimumLevel.Override("Microsoft.EntityFrameworkCore.Database.Command", Serilog.Events.LogEventLevel.Warning)
    .WriteTo.Console()
    .WriteTo.File(
    "Logs/log-.txt",
    rollingInterval: RollingInterval.Day,
    retainedFileCountLimit: 30)
    .CreateLogger();

var builder = WebApplication.CreateBuilder(args);
builder.Host.UseSerilog();

// Configuration SQL Server DBContext.
builder.Services.AddDbContext<TicketingSqlDbContext>(options =>
{
    options.UseSqlServer(builder.Configuration.GetConnectionString("SqlServer"));
});

builder.Services.AddScoped<ICategoryRepository, SqlCategoryRepository>();
builder.Services.AddScoped<ITicketRepository, SqlTicketRepository>();
builder.Services.AddScoped<IUserRepository, SqlUserRepository>();
builder.Services.AddScoped<IRefreshTokenRepository, SqlRefreshTokenRepository>();
builder.Services.AddScoped<ITokenService, TokenService>();
builder.Services.AddHostedService<OutboxProcessorBackgroundService>();

builder.Services.AddSampleDataGenerator(builder.Configuration);

//// Configuration SampleDataGenerator by code.
// builder.Services.AddSampleDataGenerator(options =>
// {
//    options.ModelPath = "models/llama.gguf";
//    options.MaxTokens = 2048;
//    options.Temperature = 0.7f;
// });

// Configuration JWT.
builder.Services.Configure<JwtOptions>(
    builder.Configuration.GetSection(JwtOptions.SectionName));

var jwt = builder.Configuration
    .GetSection(JwtOptions.SectionName)
    .Get<JwtOptions>()!;

builder.Services
    .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.MapInboundClaims = false;

        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,

            ValidIssuer = jwt.Issuer,
            ValidAudience = jwt.Audience,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwt.Key)),
        };
    });

builder.Services.AddScoped<DemoDataSeeder>();
builder.Services.Configure<DemoUserOptions>(
    builder.Configuration.GetSection("DemoUser"));

builder.Services.AddAuthorization();

builder.Services.AddControllers();

builder.Services.AddSwaggerGen(options =>
{
    options.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Name = "Authorization",
        Type = SecuritySchemeType.Http,
        Scheme = "bearer",
        BearerFormat = "JWT",
        In = ParameterLocation.Header,
        Description = "Enter: Bearer {your JWT token}",
    });

    // ✅ .NET 10 / Swashbuckle v10 : AddSecurityRequirement attend un delegate
    options.AddSecurityRequirement(document => new()
    {
        // OpenApiSecuritySchemeReference est la clé pour éviter OpenApiReference/ReferenceType
        [new OpenApiSecuritySchemeReference("Bearer", document)] = [],
    });
});

builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowWasm", policy =>
    {
        policy
            .WithOrigins("https://localhost:7124", "http://localhost:5197")
            .AllowAnyHeader()
            .AllowAnyMethod()
            .AllowCredentials();
    });
});

const string AngularCorsPolicy = "AngularClient";

builder.Services.AddCors(options =>
{
    options.AddPolicy(AngularCorsPolicy, policy =>
    {
        policy
            .WithOrigins("http://localhost:58654")
            .AllowAnyHeader()
            .AllowAnyMethod();
    });
});

var app = builder.Build();

//using (var scope = app.Services.CreateScope())
//{
//    var initializer = scope.ServiceProvider
//        .GetRequiredService<QdrantCollectionInitializer>();

//    await initializer.InitializeAsync();

//    var ingestionService = scope.ServiceProvider
//    .GetRequiredService<IDocumentIngestionService>();

//    var filePath = Path.Combine(
//        app.Environment.ContentRootPath,
//        "RagDocuments",
//        "ticketing-rules.md");

//    await ingestionService.IngestAsync(filePath);

//    var retriever = scope.ServiceProvider
//    .GetRequiredService<QdrantRagRetriever>();

//    var documents = await retriever.SearchAsync("JWT renewal failed", CancellationToken.None);
//}

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();

app.UseMiddleware<ExceptionMiddleware>();

app.UseCors("AllowWasm");
app.UseCors(AngularCorsPolicy);

app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

app.MapSampleDataEndpoints();
app.MapAiToolsEndpoints();
app.MapAiAgentEndpoints();

using (var scope = app.Services.CreateScope())
{
    var seeder = scope.ServiceProvider.GetRequiredService<DemoDataSeeder>();
    await seeder.SeedAsync();
}

app.Run();

public partial class Program
{
}
