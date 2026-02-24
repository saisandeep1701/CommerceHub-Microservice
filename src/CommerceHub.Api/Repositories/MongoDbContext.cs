using CommerceHub.Api.Configuration;
using CommerceHub.Api.Models;
using Microsoft.Extensions.Options;
using MongoDB.Driver;

namespace CommerceHub.Api.Repositories;

/// <summary>
/// Provides typed access to MongoDB collections.
/// </summary>
public class MongoDbContext
{
    private readonly IMongoDatabase _database;

    public MongoDbContext(IOptions<MongoDbSettings> settings)
    {
        var client = new MongoClient(settings.Value.ConnectionString);
        _database = client.GetDatabase(settings.Value.DatabaseName);
    }

    public IMongoCollection<Product> Products => _database.GetCollection<Product>("products");
    public IMongoCollection<Order> Orders => _database.GetCollection<Order>("orders");
}
