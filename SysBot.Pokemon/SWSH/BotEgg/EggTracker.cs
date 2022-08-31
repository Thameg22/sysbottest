using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Threading;

namespace SysBot.Pokemon
{
    public class EggTracker
    {
        [Serializable]
        public class EggStatistics
        {
            public int EggsReceived { get; set; }
            public int MatchesObtained { get; set; }
            public Dictionary<string, string> MatchLog { get; set; } = new();

            public EggStatistics() { }
        }

        public readonly EggStatistics EggStats;

        private static object _sync = new();
        private static object _syncVars = new();

        public EggTracker(EggStatistics eggStats) { EggStats = eggStats; }
        public EggTracker(string path)
        {
            if (!File.Exists(path))
            {
                lock (_sync)
                {
                    EggStats = new EggStatistics();
                    var json = JsonSerializer.Serialize(EggStats);
                    File.WriteAllText(path, json);
                    return;
                }
            }

            var str = File.ReadAllText(path);
            var eggs = JsonSerializer.Deserialize<EggStatistics>(str);
            if (eggs != null)
                EggStats = eggs;
            else
                throw new Exception("Unable to deserialize item at " + path);
        }

        public void IncrementReceived()
        {
            lock (_syncVars)
                EggStats.EggsReceived++;
        }

        public void IncrementMatches()
        {
            lock (_syncVars)
                EggStats.MatchesObtained++;
        }

        public void AddMatch(string date, string pokeform)
        {
            lock (_syncVars)
                EggStats.MatchLog.Add(date, pokeform);
        }

        public void Save(string path)
        {
            lock(_sync)
            {
                var json = JsonSerializer.Serialize(EggStats);
                File.WriteAllText(path, json);
            }
        }
    }
}
