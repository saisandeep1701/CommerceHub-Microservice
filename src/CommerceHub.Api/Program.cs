using CommerceHub.Api.Configuration;
using CommerceHub.Api.Messaging;
using CommerceHub.Api.Middleware;
using CommerceHub.Api.Repositories;
using CommerceHub.Api.Services;
using FluentValidation;

var builder = WebApplication.CreateBuilder(args);

// ---------------------------------------------------------------------------
// Configuration
// ---------------------------------------------------------------------------
builder.Services.Configure<MongoDbSettings>(
    builder.Configuration.GetSection(MongoDbSettings.SectionName));
builder.Services.Configure<RabbitMqSettings>(
    builder.Configuration.GetSection(RabbitMqSettings.SectionName));

// ---------------------------------------------------------------------------
// MongoDB
// ---------------------------------------------------------------------------
builder.Services.AddSingleton<MongoDbContext>();

// ---------------------------------------------------------------------------
// Repositories
// ---------------------------------------------------------------------------
builder.Services.AddScoped<IProductRepository, ProductRepository>();
builder.Services.AddScoped<IOrderRepository, OrderRepository>();

// ---------------------------------------------------------------------------
// Messaging
// ---------------------------------------------------------------------------
builder.Services.AddSingleton<IEventPublisher, RabbitMqEventPublisher>();

// ---------------------------------------------------------------------------
// Services
// ---------------------------------------------------------------------------
builder.Services.AddScoped<IOrderService, OrderService>();
builder.Services.AddScoped<IProductService, ProductService>();

// ---------------------------------------------------------------------------
// Validation (registers all validators from this assembly)
// ---------------------------------------------------------------------------
builder.Services.AddValidatorsFromAssemblyContaining<Program>();

// ---------------------------------------------------------------------------
// Controllers + Swagger
// ---------------------------------------------------------------------------
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new()
    {
        Title = "CommerceHub API",
        Version = "v1",
        Description = "Commerce Hub Microservice — Order processing with atomic inventory management"
    });
});

var app = builder.Build();

// ---------------------------------------------------------------------------
// Middleware Pipeline
// ---------------------------------------------------------------------------
app.UseMiddleware<GlobalExceptionMiddleware>();

app.UseSwagger();
app.UseSwaggerUI();

app.MapControllers();

app.Run();

// Make Program class accessible for integration tests
public partial class Program { }
