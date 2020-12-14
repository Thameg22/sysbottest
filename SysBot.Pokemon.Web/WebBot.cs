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

        private const int Code = 1111_7477; // while I test

        public WebBot(WebSettings settings, PokeTradeHub<PK8> hub)
        {
            Hub = hub;
            URI = settings.URIEndpoint;
            AuthID = settings.AuthID;
            AuthString = settings.AuthTokenOrString;
            WebNotifierInstance = new SignalRNotify<PK8>(AuthID, AuthString, URI);
            Task.Run(() => loopTrades(0));
            Task.Run(() => loopTrades(1));
        }

        private async void loopTrades(ulong toAdd = ulong.MaxValue)
        {
            var trainerDetail = "Berichan" + (toAdd == ulong.MaxValue ? "" : toAdd.ToString());
            var userID = toAdd == ulong.MaxValue ? 0ul : toAdd;
            var trainer = new PokeTradeTrainerInfo(trainerDetail);
            var pk = new PK8();
            while (true)
            {
                if (!Hub.Queues.GetQueue(PokeRoutineType.SeedCheck).Contains(trainerDetail))
                {
                    await Task.Delay(100).ConfigureAwait(false);

                    var notifier = new WebTradeNotifier<PK8>(pk, trainer, Code, WebNotifierInstance);
                    var detail = new PokeTradeDetail<PK8>(pk, trainer, notifier, PokeTradeType.Seed, Code, true);
                    var trade = new TradeEntry<PK8>(detail, userID, PokeRoutineType.SeedCheck, "");

                    Info.AddToTradeQueue(trade, userID, false);
                }

                await Task.Delay(1_000).ConfigureAwait(false);
            }
        }
    }
}
