using Discord;
using Discord.Commands;
using Discord.WebSocket;
using PKHeX.Core;
using SysBot.Base;
using System;
using System.Linq;
using System.Threading.Tasks;
using System.Xml.Linq;

namespace SysBot.Pokemon.Discord
{
    [Summary("Queues new Egg parent requests")]
    public class EggModule<T> : ModuleBase<SocketCommandContext> where T : PKM, new()
    {
        private static TradeQueueInfo<T> Info => SysCord<T>.Runner.Hub.Queues.Info;

        [Command("egg")]
        [Alias("parent", "setParent")]
        [Summary("Adds this Pokémon as a parent to the egg breeding queue")]
        [RequireQueueRole(nameof(DiscordManager.RolesEgg))]
        public async Task EggParentAsync([Summary("Showdown Set")][Remainder] string content)
        {
            content = ReusableActions.StripCodeBlock(content);
            var set = new ShowdownSet(content);
            var template = AutoLegalityWrapper.GetTemplate(set);
            if (set.InvalidLines.Count != 0)
            {
                var msg = $"Unable to parse Showdown Set:\n{string.Join("\n", set.InvalidLines)}";
                await ReplyAsync(msg).ConfigureAwait(false);
                return;
            }

            try
            {
                var sav = AutoLegalityWrapper.GetTrainerInfo<T>();
                var pkm = sav.GetLegal(template, out var result);
                var la = new LegalityAnalysis(pkm);
                var spec = GameInfo.Strings.Species[template.Species];
                pkm = EntityConverter.ConvertToType(pkm, typeof(T), out _) ?? pkm;
                if (pkm is not T pk || !la.Valid)
                {
                    var reason = result == "Timeout" ? $"That {spec} set took too long to generate." : $"I wasn't able to create a {spec} from that set.";
                    var imsg = $"Oops! {reason}";
                    if (result == "Failed")
                        imsg += $"\n{AutoLegalityWrapper.GetLegalizationHint(template, sav, pkm)}";
                    await ReplyAsync(imsg).ConfigureAwait(false);
                    return;
                }
                pk.ResetPartyStats();

                // Assert correct IVs
                pk.IVs = template.IVs;

                var sig = Context.User.GetFavor();
                await AddParentToQueueAsync(Context.User.Username, pk, sig, Context.User).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                LogUtil.LogSafe(ex, nameof(TradeModule<T>));
                var msg = $"Oops! An unexpected problem happened with this Showdown Set:\n```{string.Join("\n", set.GetSetLines())}```";
                await ReplyAsync(msg).ConfigureAwait(false);
            }
        }

        [Command("egg")]
        [Alias("parent", "setParent")]
        [Summary("Adds this Pokémon as a parent to the egg breeding queue")]
        [RequireQueueRole(nameof(DiscordManager.RolesEgg))]
        public async Task TradeAsyncAttach()
        {
            var sig = Context.User.GetFavor();
            await EggAsyncAttach(sig, Context.User).ConfigureAwait(false);
        }

        /*[Command("setEggSeed")]
        [Alias("eggSeed")]
        [Summary("Sets the current egg seed.")]
        [RequireSudo]
        public async Task SetEggSeed()
        {

        }*/


        [Command("eggList")]
        [Alias("dl", "dq")]
        [Summary("Prints the users in the Egg queue.")]
        [RequireSudo]
        public async Task GetListAsync()
        {
            string msg = Info.GetTradeList(PokeRoutineType.InteractiveEggFetch);
            var embed = new EmbedBuilder();
            embed.AddField(x =>
            {
                x.Name = "Pending parents";
                x.Value = msg;
                x.IsInline = false;
            });
            await ReplyAsync("These are the users who are currently waiting:", embed: embed.Build()).ConfigureAwait(false);
        }

        [Command("eggStats")]
        [Summary("Prints interactive egg fetch stats.")]
        [RequireSudo]
        public async Task GetStatsAsync()
        {
            var track = Info.Hub.Config.Egg.GetEggTracker();
            var msg = $"Eggs received: **{track.EggStats.EggsReceived}**\r\nEggs that matched criteria: **{track.EggStats.MatchesObtained}**";
            if (track.EggStats.MatchesObtained > 0) // Do not divide by zero
                msg += $"\r\nRate: **1/{track.EggStats.EggsReceived / track.EggStats.MatchesObtained}**";

            if (track.EggStats.MatchLog.Count > 0)
                msg += $"\r\n\r\nLatest Egg:\r\n{track.EggStats.MatchLog.Last()}";

            var embed = new EmbedBuilder();
            embed.AddField(x =>
            {
                x.Name = "Breeding Statistics";
                x.Value = msg;
                x.IsInline = false;
            });
            await ReplyAsync(embed: embed.Build()).ConfigureAwait(false);
        }

        [Command("eggSkip")]
        [Summary("Skips all current parent breed requests and moves to next one.")]
        [RequireSudo]
        public async Task SkipEggRequestAsync(string name = "")
        {
            var bots = SysCord<T>.Runner.Bots.Select(z => z.Bot);
            foreach (var b in bots)
            {
                if (b is not FancyEggBot x)
                    continue;
                if (!b.Connection.Name.Contains(name) && !b.Connection.Label.Contains(name))
                    continue;
                x.SkipRequested = true;
            }

            await ReplyAsync("Done.").ConfigureAwait(false);
        }

        private async Task EggAsyncAttach(RequestSignificance sig, SocketUser usr)
        {
            var attachment = Context.Message.Attachments.FirstOrDefault();
            if (attachment == default)
            {
                await ReplyAsync("No attachment provided!").ConfigureAwait(false);
                return;
            }

            var att = await NetUtil.DownloadPKMAsync(attachment).ConfigureAwait(false);
            var pk = GetRequest(att);
            if (pk == null)
            {
                await ReplyAsync("Attachment provided is not compatible with this module!").ConfigureAwait(false);
                return;
            }

            await AddParentToQueueAsync(usr.Username, pk, sig, usr).ConfigureAwait(false);
        }

        private static T? GetRequest(Download<PKM> dl)
        {
            if (!dl.Success)
                return null;
            return dl.Data switch
            {
                null => null,
                T pk => pk,
                _ => EntityConverter.ConvertToType(dl.Data, typeof(T), out _) as T,
            };
        }

        private async Task AddParentToQueueAsync(string trainerName, T pk, RequestSignificance sig, SocketUser usr)
        {
            if (!BreedingLegality.CanBreed(pk.Species, pk.Form))
            {
                await ReplyAsync("Provided Pokémon is unable to breed!").ConfigureAwait(false);
                return;
            }

            var la = new LegalityAnalysis(pk);
            if (!la.Valid)
            {
                await ReplyAsync($"{typeof(T).Name} attachment is not legal, and cannot be traded!").ConfigureAwait(false);
                return;
            }

            await QueueHelper<T>.AddToQueueAsync(Context, 0, trainerName, sig, pk, PokeRoutineType.InteractiveEggFetch, PokeTradeType.Specific, usr).ConfigureAwait(false);
        }
    }
}
