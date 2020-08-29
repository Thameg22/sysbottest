using PKHeX.Core;
using SysBot.Base;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;

namespace SysBot.Pokemon.Web
{
    public class WebTradeNotifier<T> : IPokeTradeNotifier<T> where T : PKM, new()
    {
        private T Data { get; }
        private PokeTradeTrainerInfo Info { get; }
        private int Code { get; }
        private IWebNotify<T> WebNotify { get; }
        private T Result { get; set; }
        private string OtherTrainer { get; set; } = string.Empty;

        public WebTradeNotifier(T data, PokeTradeTrainerInfo info, int code, IWebNotify<T> notifier)
        {
            Data = data;
            Info = info;
            Code = code;
            WebNotify = notifier;
            Result = new T();
        }

        public Action<PokeRoutineExecutor>? OnFinish { private get; set; }

        public void TradeInitialize(PokeRoutineExecutor routine, PokeTradeDetail<T> info)
        {
            NotifyServerOfState(WebTradeState.Initialising);
            LogUtil.LogText($"Code: {info.Code:0000 0000}");
        }

        public void TradeSearching(PokeRoutineExecutor routine, PokeTradeDetail<T> info)
        {
            NotifyServerOfState(WebTradeState.Searching, new KeyValuePair<string, string>("option", $"{info.Code:0000 0000}"));
        }

        public void TradeCanceled(PokeRoutineExecutor routine, PokeTradeDetail<T> info, PokeTradeResult msg)
        {
            OnFinish?.Invoke(routine);
            NotifyServerOfState(WebTradeState.Canceled);
        }

        public void TradeFinished(PokeRoutineExecutor routine, PokeTradeDetail<T> info, T result)
        {
            NotifyServerOfState(WebTradeState.Finished);
            Result = result;
            OnFinish?.Invoke(routine);
        }

        public void SendNotification(PokeRoutineExecutor routine, PokeTradeDetail<T> info, string message)
        {
            if (message.TryStringBetweenStrings("Trading Partner: ", ". Waiting for", out var trainerName))
            {
                OtherTrainer = trainerName;
                NotifyServerOfState(WebTradeState.FoundTrainer, new KeyValuePair<string, string>("option", trainerName));
            }
            if (message.TryStringBetweenStrings("Link Trade Code: ", "...", out var code))
                NotifyServerOfState(WebTradeState.TypingCode, new KeyValuePair<string, string>("option", code));

            if (message.Contains("Unable to calculate seeds: "))
                NotifyServerOfState(WebTradeState.Finished, new KeyValuePair<string, string>("option", OtherTrainer + ": " + message.Replace("Unable to calculate seeds: ", string.Empty)));
            if (message.Contains("This Pokémon is already shiny!"))
                NotifyServerOfState(WebTradeState.Finished, new KeyValuePair<string, string>("option", OtherTrainer + ": This Pokémon is already shiny!"));
            if (message.StartsWith("SSR"))
                NotifyServerOfState(WebTradeState.Finished, new KeyValuePair<string, string>("option", OtherTrainer + ": " + message.Substring(3)));
        }

        public void SendNotification(PokeRoutineExecutor routine, PokeTradeDetail<T> info, PokeTradeSummary message)
        {
            if (message.ExtraInfo is SeedSearchResult r)
            {
                NotifyServerOfTradeInfo(r);
                LogUtil.LogText($"Seed: {r.Seed:X16}");
            }
        }

        public void SendNotification(PokeRoutineExecutor routine, PokeTradeDetail<T> info, T result, string message)
        {
            SendNotification(routine, info, message);
        }

        private void NotifyServerOfState(WebTradeState state, params KeyValuePair<string, string>[] additionalParams)
            =>WebNotify.NotifyServerOfState(state, additionalParams);

        private void NotifyServerOfTradeInfo(SeedSearchResult r)
            => WebNotify.NotifyServerOfSeedInfo(r, Result);



        /*private void NotifyServerOfStateOld(WebTradeState state, params KeyValuePair<string, string>[] additionalParams)
        {
            var paramsToSend = new Dictionary<string, string>();
            paramsToSend.Add("wts", state.ToString().WebSafeBase64Encode());
            foreach (var p in additionalParams)
                paramsToSend.Add(p.Key, p.Value.WebSafeBase64Encode());
            NotifyServerEndpoint(paramsToSend.ToArray());
        }

        private void NotifyServerOfTradeInfoOld(SeedSearchResult r)
        {
            try
            {
                var paramsToSend = new Dictionary<string, string>();
                paramsToSend.Add("seedState", r.Type.ToString().WebSafeBase64Encode());
                paramsToSend.Add("seed", r.Seed.ToString("X16").WebSafeBase64Encode());
                paramsToSend.Add("ot", Result.OT_Name.WebSafeBase64Encode());
                paramsToSend.Add("dex", Result.Species.ToString().WebSafeBase64Encode());
                NotifyServerEndpoint(paramsToSend.ToArray());
            }
            catch { }
        }

        private void NotifyServerEndpoint(params KeyValuePair<string, string>[] urlParams)
        {
            try
            {
                var authToken = string.Format("&{0}={1}", AuthID, AuthString);
                var uriTry = encodeUriParams(URI, urlParams) + authToken;

                var request = (HttpWebRequest)WebRequest.Create(uriTry);
                request.Method = WebRequestMethods.Http.Head;
                request.Timeout = 20000;
                var response = request.GetResponse();
            }
            catch (Exception e){ LogUtil.LogText(e.Message); Environment.Exit(42069); }
        }

        private string encodeUriParams(string uriBase, params KeyValuePair<string, string>[] urlParams)
        {
            if (urlParams.Length < 1)
                return uriBase;
            if (uriBase[uriBase.Length - 1] != '?')
                uriBase += "?";
            foreach (var kvp in urlParams)
                uriBase += string.Format("{0}={1}&", kvp.Key, kvp.Value);

            // remove trailing &
            return uriBase.Remove(uriBase.Length - 1, 1);
        }*/
    }
}
