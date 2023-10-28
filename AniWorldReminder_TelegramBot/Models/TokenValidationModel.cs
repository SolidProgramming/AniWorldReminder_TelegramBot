using Org.BouncyCastle.Asn1.Mozilla;

namespace AniWorldReminder_TelegramBot.Models
{
    public class TokenValidationModel
    {
        public bool Validated { get { return Errors.Count == 0; } }
        public readonly List<TokenValidationStatus> Errors = new();
        public DateTime ExpireDate { get; set; }
    }
}
