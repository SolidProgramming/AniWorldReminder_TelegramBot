using AniWorldReminder_TelegramBot.Models.DB;
using System.Text;

namespace AniWorldReminder_TelegramBot.Misc
{
    public static class Helper
    {
        public static string GetString(byte[] reason) => Encoding.ASCII.GetString(reason);
        public static byte[] GetBytes(string reason) => Encoding.ASCII.GetBytes(reason);
        public static TokenValidationModel ValidateToken(UsersModel user, string token)
        {
            TokenValidationModel result = new();
            byte[] data = Convert.FromBase64String(token);
            byte[] _time = data.Take(8).ToArray();
            byte[] _key = data.Skip(8).Take(user.TelegramChatId.Length).ToArray();

            DateTime when = DateTime.FromBinary(BitConverter.ToInt64(_time, 0));
            result.ExpireDate = when;
            if (when < DateTime.Now)
            {
                result.Errors.Add(TokenValidationStatus.Expired);
            }

            if (GetString(_key) != user.TelegramChatId)
            {
                result.Errors.Add(TokenValidationStatus.WrongTelegramId);
            }

            return result;
        }
        public static string GenerateToken(UsersModel user)
        {
            byte[] _time = BitConverter.GetBytes(DateTime.Now.AddMinutes(10).ToBinary());
            byte[] _key = GetBytes(user.TelegramChatId);
            byte[] data = new byte[_time.Length + _key.Length];

            Buffer.BlockCopy(_time, 0, data, 0, _time.Length);
            Buffer.BlockCopy(_key, 0, data, _time.Length, _key.Length);

            return Convert.ToBase64String(data.ToArray());
        }
    }
}
