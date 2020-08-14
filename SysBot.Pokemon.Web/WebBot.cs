using NLog.Fluent;
using PKHeX.Core;
using SysBot.Base;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace SysBot.Pokemon.Web
{

    public class WebBot
    {
        private readonly PokeTradeHub<PK8> Hub;
        private TradeQueueInfo<PK8> Info => Hub.Queues.Info;

        private readonly string URI;
        private readonly string AuthID, AuthString;

        private readonly IWebNotify<PK8> WebNotifierInstance;

        private int Code = 0000_7477;

        public WebBot(WebSettings settings, PokeTradeHub<PK8> hub)
        {
            Hub = hub;
            URI = settings.URIEndpoint;
            AuthID = settings.AuthID;
            AuthString = settings.AuthTokenOrString;
            WebNotifierInstance = new WebQueryNotify<PK8>(AuthID, AuthString, URI);
            Task.Run(() => loopTrades());
        }

        private async void loopTrades()
        {
            var trainer = new PokeTradeTrainerInfo("Berichan");
            var pk = new PK8();
            while (true)
            {
                if (Hub.Queues.GetQueue(PokeRoutineType.SeedCheck).Count == 0)
                {
                    var notifier = new WebTradeNotifier<PK8>(pk, trainer, Code, WebNotifierInstance);
                    var detail = new PokeTradeDetail<PK8>(pk, trainer, notifier, PokeTradeType.Seed, Code);
                    var trade = new TradeEntry<PK8>(detail, 0ul, PokeRoutineType.SeedCheck, "");

                    Info.AddToTradeQueue(trade, 0ul, false);
                }

                while (Hub.Queues.GetQueue(PokeRoutineType.SeedCheck).Count > 0)
                {
                    await Task.Delay(1_000).ConfigureAwait(false);
                }
            }
        }
    }
}
