using Discord.Commands;
using Discord.WebSocket;
using SysBot.Base;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Discord;

namespace SysBot.Pokemon.Discord
{
    public class EchoModule : ModuleBase<SocketCommandContext>
    {
        private class EchoChannel
        {
            public readonly ulong ChannelID;
            public readonly string ChannelName;
            public readonly Action<string> Action;

            public EchoChannel(ulong channelId, string channelName, Action<string> action)
            {
                ChannelID = channelId;
                ChannelName = channelName;
                Action = action;
            }
        }

        private class EchoFileChannel
        {
            public readonly ulong ChannelID;
            public readonly string ChannelName;
            public readonly Action<string, string> Action;

            public EchoFileChannel(ulong channelId, string channelName, Action<string, string> action)
            {
                ChannelID = channelId;
                ChannelName = channelName;
                Action = action;
            }
        }

        private static readonly Dictionary<ulong, EchoChannel> Channels = new();
        private static readonly Dictionary<ulong, EchoFileChannel> FileChannels = new();

        public static void RestoreChannels(DiscordSocketClient discord, DiscordSettings cfg)
        {
            foreach (var ch in cfg.EchoChannels)
            {
                if (discord.GetChannel(ch.ID) is ISocketMessageChannel c)
                    AddEchoChannel(c, ch.ID);
            }

            foreach (var ch in cfg.EchoFileChannels)
            {
                if (discord.GetChannel(ch.ID) is ISocketMessageChannel c)
                    AddEchoChannel(c, ch.ID, true);
            }

            EchoUtil.Echo("Added echo notification to Discord channel(s) on Bot startup.");
        }

        [Command("echoHere")]
        [Summary("Makes the echo special messages to the channel.")]
        [RequireSudo]
        public async Task AddEchoAsync()
        {
            var c = Context.Channel;
            var cid = c.Id;
            if (Channels.TryGetValue(cid, out _))
            {
                await ReplyAsync("Already notifying here.").ConfigureAwait(false);
                return;
            }

            AddEchoChannel(c, cid);

            // Add to discord global loggers (saves on program close)
            SysCordSettings.Settings.EchoChannels.AddIfNew(new[] { GetReference(Context.Channel) });
            await ReplyAsync("Added Echo output to this channel!").ConfigureAwait(false);
        }

        private static void AddEchoChannel(ISocketMessageChannel c, ulong cid, bool isFile = false)
        {
            if (!isFile)
            {
                void Echo(string msg) => c.SendMessageAsync(msg);

                Action<string> l = Echo;
                EchoUtil.Forwarders.Add(l);
                var entry = new EchoChannel(cid, c.Name, l);
                Channels.Add(cid, entry);
            }
            else
            {
                void EchoFile(string msg, string file) => c.SendFileAsync(file, msg);

                Action<string, string> l = EchoFile;
                EchoUtil.FileForwarders.Add(l);
                var entry = new EchoFileChannel(cid, c.Name, l);
                FileChannels.Add(cid, entry);
            }
        }

        public static bool IsEchoChannel(ISocketMessageChannel c)
        {
            var cid = c.Id;
            return Channels.TryGetValue(cid, out _);
        }

        [Command("echoInfo")]
        [Summary("Dumps the special message (Echo) settings.")]
        [RequireSudo]
        public async Task DumpEchoInfoAsync()
        {
            foreach (var c in Channels)
                await ReplyAsync($"{c.Key} - {c.Value}").ConfigureAwait(false);
        }

        [Command("echoClear")]
        [Summary("Clears the special message echo settings in that specific channel.")]
        [RequireSudo]
        public async Task ClearEchosAsync()
        {
            var id = Context.Channel.Id;
            if (!Channels.TryGetValue(id, out var echo))
            {
                await ReplyAsync("Not echoing in this channel.").ConfigureAwait(false);
                return;
            }
            EchoUtil.Forwarders.Remove(echo.Action);
            Channels.Remove(Context.Channel.Id);
            SysCordSettings.Settings.EchoChannels.RemoveAll(z => z.ID == id);
            await ReplyAsync($"Echoes cleared from channel: {Context.Channel.Name}").ConfigureAwait(false);
        }

        [Command("echoClearAll")]
        [Summary("Clears all the special message Echo channel settings.")]
        [RequireSudo]
        public async Task ClearEchosAllAsync()
        {
            foreach (var l in Channels)
            {
                var entry = l.Value;
                await ReplyAsync($"Echoing cleared from {entry.ChannelName} ({entry.ChannelID}!").ConfigureAwait(false);
                EchoUtil.Forwarders.Remove(entry.Action);
            }
            EchoUtil.Forwarders.RemoveAll(y => Channels.Select(x => x.Value.Action).Contains(y));
            Channels.Clear();
            SysCordSettings.Settings.EchoChannels.Clear();
            await ReplyAsync("Echoes cleared from all channels!").ConfigureAwait(false);
        }

        private RemoteControlAccess GetReference(IChannel channel) => new()
        {
            ID = channel.Id,
            Name = channel.Name,
            Comment = $"Added by {Context.User.Username} on {DateTime.Now:yyyy.MM.dd-hh:mm:ss}",
        };
    }
}