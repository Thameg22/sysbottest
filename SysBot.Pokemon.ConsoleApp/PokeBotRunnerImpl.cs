using PKHeX.Core;
using SysBot.Pokemon.Discord;
using SysBot.Pokemon.Twitch;
using SysBot.Pokemon.Web;
using System.Threading;
using System.Threading.Tasks;

namespace SysBot.Pokemon
{
    /// <summary>
    /// Bot Environment implementation with Integrations added.
    /// </summary>
    public class PokeBotRunnerImpl : PokeBotRunner
    {
        public PokeBotRunnerImpl(PokeTradeHub<PK8> hub) : base(hub) { }
        public PokeBotRunnerImpl(PokeTradeHubConfig config) : base(config) { }

        private static TwitchBot? Twitch;
        private static WebBot? Web;

        protected override void AddIntegrations()
        {
            if (!string.IsNullOrWhiteSpace(Hub.Config.Discord.Token))
                AddDiscordBot(Hub.Config.Discord.Token);

            if (!string.IsNullOrWhiteSpace(Hub.Config.Twitch.Token))
                AddTwitchBot(Hub.Config.Twitch);

            if (!string.IsNullOrWhiteSpace(Hub.Config.Web.URIEndpoint))
                AddWebBot(Hub.Config.Web);
        }

        private void AddTwitchBot(TwitchSettings config)
        {
            if (Twitch != null)
                return; // already created

            if (string.IsNullOrWhiteSpace(config.Channel))
                return;
            if (string.IsNullOrWhiteSpace(config.Username))
                return;
            if (string.IsNullOrWhiteSpace(config.Token))
                return;

            Twitch = new TwitchBot(Hub.Config.Twitch, Hub);
            if (Hub.Config.Twitch.DistributionCountDown)
                Hub.BotSync.BarrierReleasingActions.Add(() => Twitch.StartingDistribution(config.MessageStart));
        }

        private void AddWebBot(WebSettings config)
        {
            if (Web != null)
                return; // already created

            Web = new WebBot(Hub.Config.Web, Hub);
        }

        private void AddDiscordBot(string apiToken)
        {
            SysCordInstance.Runner = this;
            var bot = new SysCord(Hub);
            Task.Run(() => bot.MainAsync(apiToken, CancellationToken.None));
        }
    }
}