using MySqlConnector;

namespace RushMyBookings.Crm.Data;

public static class ConnectionStringFactory
{
    public static string Build(IConfiguration configuration)
    {
        var useLocal = configuration.GetValue<bool>("Database:UseLocal");
        var section = useLocal
            ? configuration.GetSection("Database:Local")
            : configuration.GetSection("Database:Remote");

        if (section.Exists() && !string.IsNullOrWhiteSpace(section["Server"]))
        {
            return BuildFromSection(section);
        }

        var connectionName = useLocal ? "Local" : "Default";
        return configuration.GetConnectionString(connectionName)
            ?? throw new InvalidOperationException(
                $"Database configuration is missing for {(useLocal ? "local" : "remote")} mode.");
    }

    public static bool IsLocalMode(IConfiguration configuration) =>
        configuration.GetValue<bool>("Database:UseLocal");

    private static string BuildFromSection(IConfigurationSection section)
    {
        var builder = new MySqlConnectionStringBuilder
        {
            Server = section["Server"]!,
            Port = uint.TryParse(section["Port"], out var port) ? port : 3306,
            Database = section["Name"] ?? section["Database"] ?? "rushmybookings_crms",
            UserID = section["User"] ?? "root",
            Password = section["Password"] ?? string.Empty,
            SslMode = MySqlSslMode.Preferred,
            ConnectionTimeout = 15,
            Pooling = true,
            MinimumPoolSize = 2,
            MaximumPoolSize = 20
        };

        return builder.ConnectionString;
    }
}
