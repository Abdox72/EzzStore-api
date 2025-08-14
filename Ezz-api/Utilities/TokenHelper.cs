using Microsoft.AspNetCore.WebUtilities;
using System.Text;

namespace Ezz_api.Utilities
{
    public static class TokenHelper
    {
        public static string Encode(string token)
        {
            var bytes = Encoding.UTF8.GetBytes(token);
            return WebEncoders.Base64UrlEncode(bytes); // removes +, /, =
        }

        public static string Decode(string encoded)
        {
            var bytes = WebEncoders.Base64UrlDecode(encoded);
            return Encoding.UTF8.GetString(bytes);
        }
    }
}
