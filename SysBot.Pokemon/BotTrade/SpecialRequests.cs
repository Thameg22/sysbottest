using PKHeX.Core;
using PKHeX.Core.AutoMod;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace SysBot.Pokemon
{
    public static class SpecialRequests
    {
        public enum SpecialTradeType
        {
            None,
            ItemReq,
            BallReq,
            SanitizeReq,
            Shinify,
            WonderCard,
            FailReturn
        }

        private static string NamePath = Path.Combine(Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().Location), @"0names.txt"); // needed for systemctl service on linux for mono to find
        private static string ItemPath = Path.Combine(Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().Location), @"0items.txt"); // needed for systemctl service on linux for mono to find
        private static object _sync = new object();
        private static object _sync2 = new object();
        static List<int> UsableHours = new System.Collections.Generic.List<int>(new int[] { 1, 3, 5, 7, 9, 11, 13, 15, 17, 19, 21, 23 });
        static List<string> AlwaysNames { get => collectNames(); }
        static Dictionary<string, int> SpecificItemRequests { get => collectItemReqs(); }
        static int LastHour = 0;
        static Dictionary<string, int> UserListSpecialReqCount = new Dictionary<string, int>();
        static bool IsPrimeHour(int number, string trainer) => UsableHours.Contains(number) || AlwaysNames.Contains(trainer);
        static List<string> collectNames()
        {
            string[] temp = new string[] { "\n" };
            try
            {
                lock (_sync)
                {
                    var rawText = File.ReadAllText(NamePath);
                    var split = rawText.Split(new char[] { '\n' }, StringSplitOptions.RemoveEmptyEntries);
                    temp = split;
                }
            }
            catch { }
            return new List<string>(temp);
        }

        static Dictionary<string, int> collectItemReqs()
        {
            Dictionary<string, int> tmp = new Dictionary<string, int>();
            try
            {
                lock (_sync2)
                {
                    var rawText = File.ReadAllText(ItemPath);
                    var split = rawText.Split(new char[] { '\n' }, StringSplitOptions.RemoveEmptyEntries);
                    foreach (var st in split)
                    {
                        var reqs = st.Split(',');
                        tmp.Add(reqs[0], int.Parse(reqs[1]));
                    }
                }
            }
            catch { }
            return tmp;
        }

        public static SpecialTradeType CheckItemRequest(ref PK8 pk, PokeRoutineExecutor caller, PokeTradeDetail<PK8> detail, string TrainerName, SAV8SWSH sav)
        {
            var sst = SpecialTradeType.None;

            int heldItemNew = 1; // master

            var specs = SpecificItemRequests;
            if (specs.ContainsKey(TrainerName))
                heldItemNew = specs[TrainerName];

            bool skipTimeCheck = false;
            if (pk.HeldItem >= 2 && pk.HeldItem <= 4) // ultra<>pokeball
            {
                if (!IsPrimeHour(DateTime.UtcNow.Hour, TrainerName))
                {
                    detail.SendNotification(caller, "SSRName clear request will only execute during odd UTC hours!");
                    sst = SpecialTradeType.FailReturn;
                    return sst;
                }
                switch (pk.HeldItem)
                {
                    case 2: //ultra
                        pk.ClearNickname();
                        pk.OT_Name = TrainerName;
                        break;
                    case 3: //great
                        pk.OT_Name = TrainerName;
                        break;
                    case 4: //poke
                        pk.ClearNickname();
                        break;
                }

                pk.SetRecordFlags();
                pk.HeldItem = heldItemNew; //free master

                LegalizeIfNotLegal(ref pk, caller, detail, TrainerName);

                sst = SpecialTradeType.SanitizeReq;
            }
            else if (pk.Nickname.Contains("pls"))
            {
                skipTimeCheck = true;
                PK8? loaded = LoadEvent(pk.Nickname.Replace("pls", "").ToLower(), sav);

                if (loaded != null)
                    pk = loaded;
                else
                {
                    detail.SendNotification(caller, "SSRThis isn't a valid request!");
                    sst = SpecialTradeType.FailReturn;
                    return sst;
                }

                sst = SpecialTradeType.WonderCard;
            }
            else if ((pk.HeldItem >= 18 && pk.HeldItem <= 22) || pk.IsEgg || pk.HeldItem == 27) // antidote <> awakening (21) <> paralyze heal (22) <> full heal
            {
                if (!IsPrimeHour(DateTime.UtcNow.Hour, TrainerName))
                {
                    detail.SendNotification(caller, "SSRPlease wait for odd UTC hours!");
                    sst = SpecialTradeType.FailReturn;
                    return sst;
                }
                if (pk.HeldItem == 22)
                    pk.SetUnshiny();
                else
                {
                    var type = Shiny.AlwaysStar; // antidote or ice heal
                    if (pk.HeldItem == 19 || pk.HeldItem == 21 || pk.IsEgg) // burn heal or awakening
                        type = Shiny.AlwaysSquare;
                    if (pk.HeldItem == 20 || pk.HeldItem == 21 || pk.HeldItem == 27) // ice heal or awakening or fh
                        pk.IVs = new int[] { 31, 31, 31, 31, 31, 31 };
                    CommonEdits.SetShiny(pk, type);
                }

                if (pk.IsEgg)
                {
                    detail.SendNotification(caller, "SSRThis is an egg.");
                }

                LegalizeIfNotLegal(ref pk, caller, detail, TrainerName);

                if (!pk.IsEgg)
                {
                    pk.HeldItem = heldItemNew; //free master
                    pk.SetRecordFlags();
                }
                sst = SpecialTradeType.Shinify;
            }
            else if (pk.HeldItem >= 55 && pk.HeldItem <= 62) // guard spec <> x sp.def
            {
                if (!IsPrimeHour(DateTime.UtcNow.Hour, TrainerName))
                {
                    detail.SendNotification(caller, "SSRPlease wait for odd UTC hours!");
                    sst = SpecialTradeType.FailReturn;
                    return sst;
                }

                switch (pk.HeldItem)
                {
                    case 55: // guard spec
                        pk.SetLanguage(LanguageID.Japanese);
                        break;
                    case 56: // dire hit
                        pk.SetLanguage(LanguageID.English);
                        break;
                    case 57: // x atk
                        pk.SetLanguage(LanguageID.German);
                        break;
                    case 58: // x def
                        pk.SetLanguage(LanguageID.French);
                        break;
                    case 59: // x spe
                        pk.SetLanguage(LanguageID.Spanish);
                        break;
                    case 60: // x acc
                        pk.SetLanguage(LanguageID.Korean);
                        break;
                    case 61: // x spatk
                        pk.SetLanguage(LanguageID.ChineseT);
                        break;
                    case 62: // x spdef
                        pk.SetLanguage(LanguageID.ChineseS);
                        break;
                }

                pk.ClearNickname();

                LegalizeIfNotLegal(ref pk, caller, detail, TrainerName);

                pk.SetRecordFlags();
                pk.HeldItem = heldItemNew; //free master
                sst = SpecialTradeType.SanitizeReq;
            }
            else if (pk.Nickname.StartsWith("!"))
            {
                if (!IsPrimeHour(DateTime.UtcNow.Hour, TrainerName))
                {
                    detail.SendNotification(caller, "SSRItem request will only execute during odd UTC hours!");
                    sst = SpecialTradeType.FailReturn;
                    return sst;
                }
                var itemLookup = pk.Nickname.Substring(1).Replace(" ", string.Empty);
                GameStrings strings = GameInfo.GetStrings(GameLanguage.DefaultLanguage);
                var items = (string[])strings.GetItemStrings(8, GameVersion.SWSH);
                int item = Array.FindIndex(items, z => z.Replace(" ", string.Empty).StartsWith(itemLookup, StringComparison.OrdinalIgnoreCase));
                if (item < 0)
                {
                    detail.SendNotification(caller, "SSRItem request was invalid. Check spelling & gen.");
                    return sst;
                }

                pk.HeldItem = item;

                LegalizeIfNotLegal(ref pk, caller, detail, TrainerName);

                sst = SpecialTradeType.ItemReq;
            }
            else if (pk.Nickname.StartsWith("?") || pk.Nickname.StartsWith("？"))
            {
                if (!IsPrimeHour(DateTime.UtcNow.Hour, TrainerName))
                {
                    detail.SendNotification(caller, "SSRBall request will only execute during odd UTC hours!");
                    sst = SpecialTradeType.FailReturn;
                    return sst;
                }
                var itemLookup = pk.Nickname.Substring(1).Replace(" ", string.Empty);
                GameStrings strings = GameInfo.GetStrings(GameLanguage.DefaultLanguage);
                var balls = strings.balllist;

                int item = Array.FindIndex(balls, z => z.Replace(" ", string.Empty).StartsWith(itemLookup, StringComparison.OrdinalIgnoreCase));
                if (item < 0)
                {
                    detail.SendNotification(caller, "SSRBall request was invalid. Check spelling & gen.");
                    return sst;
                }

                pk.Ball = item;

                LegalizeIfNotLegal(ref pk, caller, detail, TrainerName);

                sst = SpecialTradeType.BallReq;
            }
            else
                return sst;

            // just for that one edge case
            if (!IsPrimeHour(DateTime.UtcNow.Hour, TrainerName) && !skipTimeCheck)
            {
                return SpecialTradeType.None;
            }

            // success but prevent overuse which causes connection errors
            if (DateTime.UtcNow.Hour != LastHour)
            {
                LastHour = DateTime.UtcNow.Hour;
                UserListSpecialReqCount.Clear();
            }
            if (UserListSpecialReqCount.ContainsKey(TrainerName))
                UserListSpecialReqCount[TrainerName] = UserListSpecialReqCount[TrainerName] + 1;
            else
                UserListSpecialReqCount.Add(TrainerName, 1);

            int limit = sst == SpecialTradeType.Shinify ? 3 : 2;

            if (UserListSpecialReqCount[TrainerName] >= limit)
            {
                if (!AlwaysNames.Contains(TrainerName))
                {
                    caller.Log($"Softbanned {TrainerName}.");
                    detail.SendNotification(caller, $"SSRToo many special requests! Please wait until {(LastHour + 2) % 24}:00 UTC.");
                    return SpecialTradeType.FailReturn;
                }
            }

            if (!pk.IsEgg)
                pk.ClearNickname();
            return sst;
        }

        private static PK8? LoadEvent(string v, SAV8SWSH sav)
        {
            PK8? toRet = null;
            byte[] wc = new byte[1];
            string type = "wc8";

            string pathwc = Path.Combine("wc", v + ".wc8");
            if (File.Exists(pathwc)) { wc = File.ReadAllBytes(pathwc); type = "wc8"; }
            pathwc = Path.Combine("wc", v + ".wc7");
            if (File.Exists(pathwc)) { wc = File.ReadAllBytes(pathwc); type = "wc7"; }
            pathwc = Path.Combine("wc", v + ".wc6");
            if (File.Exists(pathwc)) { wc = File.ReadAllBytes(pathwc); type = "wc6"; }
            pathwc = Path.Combine("wc", v + ".pgf");
            if (File.Exists(pathwc)) { wc = File.ReadAllBytes(pathwc); type = "pgf"; }

            var loadedwc = LoadWC(wc, type);
            if (loadedwc != null)
            {
                var pkloaded = loadedwc.ConvertToPKM(sav);
                
                if (!pkloaded.SWSH)
                {
                    pkloaded = PKMConverter.ConvertToType(pkloaded, typeof(PK8), out _);
                    if (pkloaded != null)
                    {
                        pkloaded.CurrentHandler = 1;
                        QuickLegalize(ref pkloaded);
                    }
                }
                if (pkloaded != null)
                    toRet = (PK8)pkloaded;
            }

            return toRet;
        }

        private static MysteryGift? LoadWC(byte[] data, string suffix = "wc8")
        {
            return suffix switch
            {
                "wc8" => new WC8(data),
                "wc7" => new WC7(data),
                "wc6" => new WC6(data),
                "pgf" => new PGF(data),
                _ => null
            };
        }

        private static void QuickLegalize(ref PKM pkm)
        {
            var la = new LegalityAnalysis(pkm);
            if (!la.Valid)
                pkm = pkm.LegalizePokemon();
        }

        private static void LegalizeIfNotLegal(ref PK8 pkm, PokeRoutineExecutor caller, PokeTradeDetail<PK8> detail, string trainerName)
        {
            var tempPk = pkm.Clone();

            var la = new LegalityAnalysis(pkm);
            if (!la.Valid)
            {
                detail.SendNotification(caller, "SSRThis request isn't legal! Attemping to legalize...");
                caller.Log(la.Report());
                pkm = (PK8)pkm.LegalizePokemon();
            }
            else
                return;

            pkm.OT_Name = tempPk.OT_Name;

            la = new LegalityAnalysis(pkm);
            if (!la.Valid)
            {
                pkm = (PK8)pkm.LegalizePokemon();
            }
        }
    }
}
