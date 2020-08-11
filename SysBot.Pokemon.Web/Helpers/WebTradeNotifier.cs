using PKHeX.Core;
using SysBot.Base;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text;

namespace SysBot.Pokemon.Web
{
    public class WebTradeNotifier<T> : IPokeTradeNotifier<T> where T : PKM, new()
    {
        private T Data { get; }
        private PokeTradeTrainerInfo Info { get; }
        private int Code { get; }
        private string URI { get; }

        public WebTradeNotifier(T data, PokeTradeTrainerInfo info, int code, string uri)
        {
            Data = data;
            Info = info;
            Code = code;
            URI = uri;

            LogUtil.LogText("Starting new trade.");
        }

        public Action<PokeRoutineExecutor>? OnFinish { private get; set; }

        public void TradeInitialize(PokeRoutineExecutor routine, PokeTradeDetail<T> info)
        {
            NotifyServerOfState(WebTradeState.Initialising, new KeyValuePair<string, string>("code", $"{info.Code:0000 0000}"));
            LogUtil.LogText($"Code: {info.Code:0000 0000}");
        }

        public void TradeSearching(PokeRoutineExecutor routine, PokeTradeDetail<T> info)
        {
            NotifyServerOfState(WebTradeState.Searching);
        }

        public void TradeCanceled(PokeRoutineExecutor routine, PokeTradeDetail<T> info, PokeTradeResult msg)
        {
            OnFinish?.Invoke(routine);
            NotifyServerOfState(WebTradeState.Canceled);
        }

        public void TradeFinished(PokeRoutineExecutor routine, PokeTradeDetail<T> info, T result)
        {
            OnFinish?.Invoke(routine);
            NotifyServerOfState(WebTradeState.Finished);
        }

        public void SendNotification(PokeRoutineExecutor routine, PokeTradeDetail<T> info, string message)
        {
            
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
            
        }

        private void NotifyServerOfState(WebTradeState state, params KeyValuePair<string, string>[] additionalParams)
        {
            var paramsToSend = new Dictionary<string, string>();
            paramsToSend.Add("wts", state.ToString().WebSafeBase64Encode());
            foreach (var p in additionalParams)
                paramsToSend.Add(p.Key, p.Value);
            NotifyServerEndpoint(paramsToSend.ToArray());
        }

        private void NotifyServerOfTradeInfo(SeedSearchResult r)
        {
            var paramsToSend = new Dictionary<string, string>();
            paramsToSend.Add("seedState", r.Type.ToString().WebSafeBase64Encode());
            paramsToSend.Add("seed", r.Seed.ToString("X16").WebSafeBase64Encode());
            paramsToSend.Add("ot", Data.OT_Name.WebSafeBase64Encode());
            paramsToSend.Add("dex", Data.Species.ToString().WebSafeBase64Encode());
            NotifyServerEndpoint(paramsToSend.ToArray());
        }

        private void NotifyServerEndpoint(params KeyValuePair<string, string>[] urlParams)
        {
            try
            {
                var uriTry = encodeUriParams(URI, urlParams);

                var request = (HttpWebRequest)WebRequest.Create(uriTry);
                //request.Method = "POST";
                var response = (HttpWebResponse)request.GetResponse();
                var success = response.StatusCode == HttpStatusCode.OK;
            }
            catch { }
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
        }
    }
}
