using MongoDB.Bson.Serialization.Conventions;
using MongoDB.Driver;
using SyncBook.Server.Models;

namespace SyncBook.Server.Data;

public class MongoDbContext
{
    private readonly IMongoDatabase _database;
    private readonly IMongoCollection<Business> _businesses;

    static MongoDbContext()
    {
        var conventionPack = new ConventionPack
        {
            new CamelCaseElementNameConvention(),
            new IgnoreExtraElementsConvention(true)
        };
        ConventionRegistry.Register("SyncBookConventions", conventionPack, _ => true);
    }

    public MongoDbContext(IConfiguration configuration)
    {
        var connectionString = configuration["MongoDb:ConnectionString"]
            ?? throw new InvalidOperationException("MongoDb:ConnectionString is not configured.");
        var databaseName = configuration["MongoDb:DatabaseName"]
            ?? throw new InvalidOperationException("MongoDb:DatabaseName is not configured.");
        var businessesCollection = configuration["MongoDb:BusinessesCollectionName"] ?? "businesses";

        var client = new MongoClient(connectionString);
        _database = client.GetDatabase(databaseName);
        _businesses = _database.GetCollection<Business>(businessesCollection);
    }

    public IMongoCollection<User> Users => _database.GetCollection<User>("users");

    public IMongoCollection<Business> Businesses => _businesses;

    public IMongoCollection<Appointment> Appointments => _database.GetCollection<Appointment>("appointments");

    public IMongoCollection<StaffMember> StaffMembers =>
        _database.GetCollection<StaffMember>("staffcollection");

    public IMongoCollection<PasswordResetToken> PasswordResetTokens =>
        _database.GetCollection<PasswordResetToken>("passwordResetTokens");

    public IMongoCollection<VerificationCode> VerificationCodes =>
        _database.GetCollection<VerificationCode>("verificationCodes");
}
