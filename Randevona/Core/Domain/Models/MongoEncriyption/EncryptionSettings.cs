using Domain.Models.Shared.Settings;

namespace Domain.Models.MongoEncriyption
{
    public class EncryptionSettings : ISettings
    {
        public string Key { get; set; } = string.Empty;
    }
}
