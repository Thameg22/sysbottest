using System;
using System.ComponentModel;
using System.Linq;

namespace SysBot.Pokemon
{
    public class WebSettings
    {
        private const string Network = nameof(Network);
        public override string ToString() => "Web and Uri Endpoint Settings";

        [Category(Network), Description("HTTP or HTTPS Endpoint")]
        public string URIEndpoint { get; set; } = string.Empty;
    }
}
