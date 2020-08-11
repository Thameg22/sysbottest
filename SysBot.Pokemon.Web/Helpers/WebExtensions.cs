using System;
using System.Collections.Generic;
using System.Text;

namespace SysBot.Pokemon.Web
{
    public static class WebExtensions
    {
        static readonly char[] padding = { '=' };

        public static string WebSafeBase64Encode(this string str)
        {
            var bytes = Encoding.UTF8.GetBytes(str);
            return Convert.ToBase64String(bytes)
                .TrimEnd(padding).Replace('+', '-').Replace('/', '_'); 
        }

        public static string WebSafeBase64Decode(this string str)
        {
            string incoming = str
                .Replace('_', '/').Replace('-', '+');
            switch (str.Length % 4)
            {
                case 2: incoming += "=="; break;
                case 3: incoming += "="; break;
            }
            byte[] bytes = Convert.FromBase64String(incoming);
            return Encoding.UTF8.GetString(bytes);
        }

    }
}
