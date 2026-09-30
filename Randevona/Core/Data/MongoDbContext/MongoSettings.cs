using Domain.Models.Shared.Settings;

namespace Data.MongoDbContext
{
    public class MongoSettings : ISettings
    {
        public string ConnectionString { get; set; } = string.Empty;
        public string DatabaseName { get; set; } = string.Empty;
        public int StartupTimeoutSeconds { get; set; } = 30;

        public void Validate()
        {
            if (string.IsNullOrWhiteSpace(ConnectionString))
                throw new InvalidOperationException("MongoSettings:ConnectionString is required.");
            if (string.IsNullOrWhiteSpace(DatabaseName))
                throw new InvalidOperationException("MongoSettings:DatabaseName is required.");
            if (StartupTimeoutSeconds < 1 || StartupTimeoutSeconds > 120)
                throw new InvalidOperationException("MongoSettings:StartupTimeoutSeconds must be between 1 and 120.");
        }
    }
}
