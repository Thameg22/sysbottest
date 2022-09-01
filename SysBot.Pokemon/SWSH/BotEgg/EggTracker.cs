using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;

namespace SysBot.Pokemon
{
    public class EggTracker
    {
        [Serializable]
        public class EggCollectionEntry
        {
            public string CollectionDate { get; set; } = string.Empty;
            public string FileName { get; set; } = string.Empty;
            public int Attempts { get; set; } = 0;
            public string Seed { get; set; } = string.Empty;
            public string ShowDownSet { get; set; } = string.Empty;
            public string User { get; set; } = string.Empty;

            public EggCollectionEntry() { }

            public EggCollectionEntry(string collectionDate, string fileName, int attempts, string seed, string showDownSet, string user)
            {
                CollectionDate = collectionDate;
                FileName = fileName;
                Attempts = attempts;
                Seed = seed;
                ShowDownSet = showDownSet;
                User = user;
            }

            public override string ToString()
            {
                var sb = new StringBuilder();
                var date = DateTime.Parse(CollectionDate, CultureInfo.InvariantCulture);
                sb.AppendLine($"[{ShowDownSet.Split('\n')[0]}]");
                sb.AppendLine($"Received: {date:D} at {date:t}");
                sb.AppendLine($"Requester: {User}");
                sb.AppendLine($"Attempts: {Attempts}");
                sb.AppendLine(string.Empty);
                sb.AppendLine($"Seed: {Seed}");
                sb.AppendLine($"FileName: {FileName}");

                return sb.ToString();
            }
        }

        [Serializable]
        public class EggStatistics
        {
            public int EggsReceived { get; set; }
            public int MatchesObtained { get; set; }
            public List<EggCollectionEntry> MatchLog { get; set; } = new();

            public EggStatistics() { }

            public EggStatistics(int eggsReceived, int matchesObtained, List<EggCollectionEntry> matchLog)
            {
                EggsReceived = eggsReceived;
                MatchesObtained = matchesObtained;
                MatchLog = matchLog;
            }

            public EggCollectionEntry? GetLatest()
            {
                if (MatchLog.Count == 0)
                    return null;
                return MatchLog.Last();
            }

            public (EggCollectionEntry?, EggCollectionEntry?) GetLeastMostAttempts()
            {
                if (MatchLog.Count == 0)
                    return (null, null);

                var ordered = MatchLog.OrderBy(x => x.Attempts);
                return (MatchLog.First(), MatchLog.Last());
            }
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
                    var json = JsonSerializer.Serialize(EggStats, new JsonSerializerOptions()
                    {
                        WriteIndented = true,
                    });
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

        public void AddMatch(EggCollectionEntry collection)
        {
            lock (_syncVars)
                EggStats.MatchLog.Add(collection);
        }

        public void Save(string path)
        {
            lock(_sync)
            {
                var json = JsonSerializer.Serialize(EggStats, new JsonSerializerOptions()
                {
                    WriteIndented = true,
                });
                File.WriteAllText(path, json);
            }
        }
    }
}
