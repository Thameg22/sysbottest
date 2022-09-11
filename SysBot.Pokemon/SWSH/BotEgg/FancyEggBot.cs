using PKHeX.Core;
using System;
using System.Threading;
using System.Threading.Tasks;
using System.Linq;
using SysBot.Base;
using static SysBot.Base.SwitchButton;
using static SysBot.Base.SwitchStick;
using static SysBot.Pokemon.PokeDataOffsets;
using static System.Buffers.Binary.BinaryPrimitives;
using System.Globalization;
using System.Collections.Generic;
using System.IO;

namespace SysBot.Pokemon
{
    public class FancyEggBot : PokeRoutineExecutor8, IEncounterBot
    {

        private readonly PokeTradeHub<PK8> Hub;
        private readonly IDumper DumpSetting;
        private readonly int[] DesiredMinIVs;
        private readonly int[] DesiredMaxIVs;
        private readonly EggSettings Settings;
        private EggTracker Tracker => Settings.GetEggTracker();

        public ICountSettings Counts => Settings;

        public DayCareStructure DayCare { get; private set; } = default!;
        public PokeTradeDetail<PK8> CurrentSet { get; private set; } = default!;
        public SAV8SWSH CurrentSave { get; private set; } = default!;

        public FancyEggBot(PokeBotState cfg, PokeTradeHub<PK8> hub) : base(cfg)
        {
            Hub = hub;
            Settings = Hub.Config.Egg;
            DumpSetting = Hub.Config.Folder;
            StopConditionSettings.InitializeTargetIVs(Hub.Config, out DesiredMinIVs, out DesiredMaxIVs);
        }

        private int currentEncounterCount;
        private int partySlotEgg;

        private const int InjectBox = 0;
        private const int InjectSlot = 0;
        private const int PartySizeRequired = 6;

        private static readonly PK8 Blank = new();

        public override async Task MainLoop(CancellationToken token)
        {
            await InitializeHardware(Hub.Config.Egg, token).ConfigureAwait(false);

            Log("Identifying trainer data of the host console.");
            CurrentSave = await IdentifyTrainer(token).ConfigureAwait(false);

            await SetupBoxAndPartyState(token).ConfigureAwait(false);
            while (await SetupDaycare(token).ConfigureAwait(false) == null) { }

            Log("Starting main FancyEggBot loop.");
            Config.IterateNextRoutine();
            while (!token.IsCancellationRequested && Config.NextRoutineType == PokeRoutineType.InteractiveEggFetch)
            {
                try
                {
                    if (!await InnerLoop(token).ConfigureAwait(false))
                        break;

                    while (CurrentSet == default(PokeTradeDetail<PK8>))
                        await SetupDaycare(token).ConfigureAwait(false);
                }
#pragma warning disable CA1031 // Do not catch general exception types
                catch (Exception e)
#pragma warning restore CA1031 // Do not catch general exception types
                {
                    Log(e.Message);
                }
            }

            Log($"Ending {nameof(EggBot)} loop.");
            await HardStop().ConfigureAwait(false);
        }

        public override async Task HardStop()
        {
            // If aborting the sequence, we might have the stick set at some position. Clear it just in case.
            await SetStick(LEFT, 0, 0, 0, CancellationToken.None).ConfigureAwait(false); // reset
            await CleanExit(Hub.Config.Trade, CancellationToken.None).ConfigureAwait(false);
        }

        /// <summary>
        /// Return true if we need to stop looping.
        /// </summary>
        private async Task<bool> InnerLoop(CancellationToken token)
        {
            // Walk a step left, then right => check if egg was generated on this attempt.
            // Repeat until an egg is generated.

            if (SkipRequested)
            {
                Log("Skipped!");
                SkipRequested = false;
                CurrentSet.TradeFinished(this, Blank);
                await SetupDaycare(token).ConfigureAwait(false);
                return true;
            }

            var attempts = await StepUntilEgg(token).ConfigureAwait(false);
            if (attempts < 0) // aborted
            {
                var msgFail = $"Trainer <@{CurrentSet.Trainer.ID}> ({CurrentSet.Trainer.TrainerName}) unfortunately failed to provide a Pokémon that would breed.";
                Log(msgFail);
                CurrentSet.SendNotification(this, "Unfortunately your requested parent failed to breed. Your parent request has been removed.");
                CurrentSet.TradeFinished(this, Blank);
                EchoUtil.Echo(msgFail);
                await SetupDaycare(token).ConfigureAwait(false);
                return true;
            }

            var seed = await FetchDaycareSeed(token).ConfigureAwait(false);
            Log($"Egg available after {attempts+1} attempts! Seed: {seed:X16}. Clearing destination slot.");
            await SetBoxPokemon(Blank, InjectBox, InjectSlot, token).ConfigureAwait(false);

            for (int i = 0; i < 6; i++)
                await Click(A, 0_400, token).ConfigureAwait(false);

            // Safe to mash B from here until we get out of all menus.
            while (!await IsOnOverworld(Hub.Config, token).ConfigureAwait(false))
                await Click(B, 0_400, token).ConfigureAwait(false);

            Log("Egg received. Checking details.");
            var pk = await ReadBoxPokemon(InjectBox, InjectSlot, token).ConfigureAwait(false);
            if (pk.Species == 0)
            {
                Log("Invalid data detected in destination slot. Restarting loop.");
                return true;
            }

            currentEncounterCount++;
            var print = Hub.Config.StopConditions.GetPrintName(pk);
            Log($"Encounter: {currentEncounterCount}{Environment.NewLine}{print}{Environment.NewLine}");
            Settings.AddCompletedEggs();

            Tracker.IncrementReceived();
            if (Tracker.EggStats.EggsReceived % 10 == 0)
                Tracker.Save(EggSettings.EggTrackerFileName);

            WriteToFile(pk, currentEncounterCount);

            if (!StopConditionSettings.EncounterFound(pk, DesiredMinIVs, DesiredMaxIVs, Hub.Config.StopConditions, null))
                return true;

            if (DumpSetting.Dump && !string.IsNullOrEmpty(DumpSetting.DumpFolder))
            {
                var fn = DumpPokemon(DumpSetting.DumpFolder, "fancyegg", pk);
                var msgSucc = $"successfully bred a criteria-matched {(pk.IsShiny?"shiny " : string.Empty)}egg after {currentEncounterCount} attempts!\r\n```\r\n{print}\r\n```\r\nSeed: {seed:X16}";
                EchoUtil.EchoFile(CurrentSet.Trainer.ID == 0 ? msgSucc : $"Trainer <@{CurrentSet.Trainer.ID}> ({CurrentSet.Trainer.TrainerName}) has {msgSucc}", fn);
                CurrentSet.SendNotification(this, pk, $"Hey {CurrentSet.Trainer.TrainerName}! I've " + msgSucc);
            }

            // no need to take a video clip of us receiving an egg.
            var mode = Settings.ContinueAfterMatch;
            var msg = $"Result found with seed {seed:X16}!\n{print}\n" + mode switch
            {
                ContinueAfterMatch.Continue => "Continuing...",
                ContinueAfterMatch.PauseWaitAcknowledge => "Waiting for instructions to continue.",
                ContinueAfterMatch.StopExit => "Stopping routine execution; restart the bot to search again.",
                _ => throw new ArgumentOutOfRangeException(),
            };

            if (!string.IsNullOrWhiteSpace(Hub.Config.StopConditions.MatchFoundEchoMention))
                msg = $"{Hub.Config.StopConditions.MatchFoundEchoMention} {msg}";
            EchoUtil.Echo(msg);
            Log(msg);

            // Update stats
            Tracker.AddMatch(new(DateTime.UtcNow.ToString("o", CultureInfo.InvariantCulture), Util.CleanFileName(pk.FileName), currentEncounterCount, seed.ToString("X16"), ShowdownParsing.GetShowdownText(pk), $"{CurrentSet.Trainer.TrainerName}-{CurrentSet.Trainer.ID}"));
            Tracker.IncrementMatches();
            Tracker.Save(EggSettings.EggTrackerFileName);

            // Hatch if requested
            if (Settings.InteractiveBotShowEggHatchVisual)
                await HatchEgg(pk, token).ConfigureAwait(false);

            if (mode == ContinueAfterMatch.StopExit)
                return false;
            if (mode == ContinueAfterMatch.Continue)
            {
                CurrentSet.TradeFinished(this, Blank);
                await SetupDaycare(token).ConfigureAwait(false);
                return true;
            }

            IsWaiting = true;
            while (IsWaiting)
                await Task.Delay(1_000, token).ConfigureAwait(false);
            return false;
        }

        private async Task SetupBoxAndPartyState(CancellationToken token)
        {
            await SetCurrentBox(0, token).ConfigureAwait(false);

            var existing = await ReadBoxPokemon(InjectBox, InjectSlot, token).ConfigureAwait(false);
            if (existing.Species != 0 && existing.ChecksumValid)
            {
                Log("Destination slot is occupied! Dumping the Pokémon found there...");
                DumpPokemon(DumpSetting.DumpFolder, "saved", existing);
            }

            Log("Clearing destination slot to start the bot.");
            await SetBoxPokemon(Blank, InjectBox, InjectSlot, token).ConfigureAwait(false);

            Log("Fetching initial daycare data.");
            var daycareData = await Connection.ReadBytesAsync(DayCare_Start, DayCareStructure.DAYCARE_MAIN_SIZE, token).ConfigureAwait(false);
            DayCare = new DayCareStructure(daycareData);
            Log(DayCare.GetSummary());

            if (Settings.InteractiveBotShowEggHatchVisual)
            {
                var party = await FetchParty(token).ConfigureAwait(false);
                partySlotEgg = party.Length;
                if (partySlotEgg < PartySizeRequired)
                    Log($"Your party needs to have {PartySizeRequired} to use the egg hatching visual functionality, otherwise your empty slots will corrupt.");

                existing = new PK8(await SwitchConnection.PointerPeek(BoxFormatSlotSize, PartySlotPointers[partySlotEgg - 1], token).ConfigureAwait(false));
                if (existing.Species != 0 && existing.ChecksumValid)
                {
                    Log("Dumping the Pokémon found at destination party slot...");
                    DumpPokemon(DumpSetting.DumpFolder, "saved", existing);
                }
            }
        }

        private async Task<PokeTradeDetail<PK8>?> SetupDaycare(CancellationToken token)
        {
            CurrentSet = default!;

            (var detail, var prio) = GetTradeData(PokeRoutineType.InteractiveEggFetch);
            if (detail == null || !BreedingLegality.CanBreed(detail.TradeData.Species, detail.TradeData.Form))
            {
                Log($"Taking a break " + detail == null ? string.Empty : $"{detail?.TradeData.Nickname} cannot breed.");
                await Idle(token).ConfigureAwait(false);
                return null;
            }

            var ditto = FetchClosestDitto(detail.TradeData);
            if (ditto == null)
            {
                Log("No valid Ditto found!");
                await Task.Delay(1_000).ConfigureAwait(false);
                return null;
            }

            // Mutate ditto language if required for increased shiny odds
            if (detail.TradeData.Language == ditto.Language)
                ditto.Language = detail.TradeData.Language == 1 ? 2 : 1;

            BreedingLegality.EnsureCorrectHeldItem(detail.TradeData);
            DayCare.OccupySlot(0, detail.TradeData);
            DayCare.OccupySlot(1, ditto);

            await Connection.WriteBytesAsync(DayCare.GetData(), DayCare_Start, token).ConfigureAwait(false);

            var msg = $"Starting new breed request. Setup daycare with\r\n{ShowdownParsing.GetShowdownText(detail.TradeData)}\r\n\r\n{ShowdownParsing.GetShowdownText(ditto)}";
            Log(msg);
            EchoUtil.Echo(msg);

            CurrentSet = detail;
            CurrentSet.SendNotification(this, $"I'm starting your breed request! {msg}");
            CurrentSet.IsProcessing = true;

            currentEncounterCount = 0;

            return detail;
        }

        protected virtual async Task Idle(CancellationToken token)
        {
            await Task.Delay(1_000).ConfigureAwait(false);
        }

        protected virtual (PokeTradeDetail<PK8>? detail, uint priority) GetTradeData(PokeRoutineType type)
        {
            if (Hub.Queues.TryDequeue(type, out var detail, out var priority))
                return (detail, priority);
            if (Hub.Queues.TryDequeueLedy(out detail))
                return (detail, PokeTradePriorities.TierFree);
            return (null, PokeTradePriorities.TierFree);
        }

        private bool IsWaiting;
        public void Acknowledge() => IsWaiting = false;
        public bool SkipRequested { get; set; } = false;

        private async Task<int> StepUntilEgg(CancellationToken token)
        {
            Log("Walking around until an egg is ready...");
            int attempts = 0;
            while (!token.IsCancellationRequested && Config.NextRoutineType == PokeRoutineType.InteractiveEggFetch)
            {
                await SetEggStepCounter(token).ConfigureAwait(false);

                // Walk Diagonally Left
                await SetStick(LEFT, -19000, 19000, 0_500, token).ConfigureAwait(false);
                await SetStick(LEFT, 0, 0, 500, token).ConfigureAwait(false); // reset

                // Walk Diagonally Right, slightly longer to ensure we stay at the Daycare lady.
                await SetStick(LEFT, 19000, 19000, 0_550, token).ConfigureAwait(false);
                await SetStick(LEFT, 0, 0, 500, token).ConfigureAwait(false); // reset

                bool eggReady = await IsEggReady(token).ConfigureAwait(false);
                if (eggReady)
                    return attempts;

                attempts++;
                if (attempts % 10 == 0)
                    Log($"Tried {attempts} times, still no egg.");

                if (attempts > 10)
                    await Click(B, 500, token).ConfigureAwait(false);

                if (attempts > 100)
                    break;
            }

            return -1; // aborted
        }

        private async Task HatchEgg(PK8 egg, CancellationToken token)
        {
            if (!Settings.InteractiveBotShowEggHatchVisual || partySlotEgg < PartySizeRequired)
            {
                Log("Bot is not set up for egg hatching, skipping...");
                return;
            }

            if (!egg.IsEgg)
            {
                Log(Util.CleanFileName(egg.FileName) + " is not an egg, skipping...");
                return;
            }

            Log("Hatching egg...");

            egg.CurrentFriendship = 0; // Hatch counter

            // EC matching gives us the visual, party will still corrupt.
            var ecb = await SwitchConnection.PointerPeek(4, PartySlotPointers[partySlotEgg - 1], token).ConfigureAwait(false);
            var ec = BitConverter.ToUInt32(ecb, 0);
            egg.EncryptionConstant = ec;
            egg.RefreshChecksum();
            egg.ForcePartyData();

            // Inject the egg
            await SwitchConnection.PointerPoke(egg.EncryptedPartyData, PartySlotPointers[partySlotEgg - 1], token).ConfigureAwait(false);

            await SetStick(LEFT, -19000, 19000, 0_500, token).ConfigureAwait(false);
            await SetStick(LEFT, 0, 0, 500, token).ConfigureAwait(false); // reset

            // oh?
            await Click(A, 18_000, token).ConfigureAwait(false);

            while (!await IsOnOverworld(Hub.Config, token).ConfigureAwait(false))
                await Click(B, 0_500, token).ConfigureAwait(false);

            await SetStick(LEFT, 19000, 19000, 0_400, token).ConfigureAwait(false);
            await SetStick(LEFT, 0, 0, 500, token).ConfigureAwait(false); // reset
        }

        private async Task<PK8[]> FetchParty(CancellationToken token)
        {
            var partySlots = new List<PK8>();
            var partyCount = (await SwitchConnection.PointerPeek(1, PartySizePointer, token).ConfigureAwait(false))[0];
            for (int i = 0; i < partyCount; i++)
            {
                partySlots.Add(new PK8(await SwitchConnection.PointerPeek(BoxFormatSlotSize, PartySlotPointers[i], token).ConfigureAwait(false)));
            }

            return partySlots.ToArray();
        }

        public async Task<bool> IsEggReady(CancellationToken token)
        {
            // Read a single byte of the Daycare metadata to check the IsEggReady flag.
            var data = await Connection.ReadBytesAsync(DayCare_Route5_Egg_Is_Ready, 1, token).ConfigureAwait(false);
            return data[0] == 1;
        }

        public async Task SetEggStepCounter(CancellationToken token)
        {
            // Set the step counter in the Daycare metadata to 180. This is the threshold that triggers the "Should I create a new egg" subroutine.
            // When the game executes the subroutine, it will generate a new seed and set the IsEggReady flag.
            // Just setting the IsEggReady flag won't refresh the seed; we want a different egg every time.
            var data = new byte[] { 0xB4, 0, 0, 0 }; // 180
            await Connection.WriteBytesAsync(data, DayCare_Route5_Step_Counter, token).ConfigureAwait(false);
        }

        public async Task<ulong> FetchDaycareSeed(CancellationToken token)
        {
            var data = await Connection.ReadBytesAsync(DayCare_Start + DayCareStructure.DAYCARE_MAIN_SIZE + 0x6, sizeof(ulong), token).ConfigureAwait(false);
            return ReadUInt64LittleEndian(data);
        }

        public async Task WriteSeed(ulong seed, CancellationToken token)
        {
            var data = new byte[8];
            WriteUInt64LittleEndian(data, seed);
            await Connection.WriteBytesAsync(data, DayCare_Start + DayCareStructure.DAYCARE_MAIN_SIZE + 0x6, token);
        }

        public PK8? FetchClosestDitto(PK8 match)
        {
            var dittos = Hub.Ledy.Pool.Where(p => p.Species == 132);
            if (!dittos.Any())
                return null;

            PK8? closest = null;
            int closestDistance = int.MaxValue;
            foreach (var meta in dittos)
            {
                var dist = IVDistance(meta, match);
                if (dist < closestDistance)
                {
                    closest = meta;
                    closestDistance = dist;
                }
            }
            return closest;
        }

        private int IVDistance(PK8 pk1, PK8 pk2)
        {
            int distance = 0;
            for (int i = 0; i < pk1.IVs.Length; ++i)
                distance += Math.Abs(pk1.IVs[i] - pk2.IVs[i]);
            return distance;
        }

        private void WriteToFile(PK8 pk, int numAttempts)
        {
            try
            {
                var fileName = Connection.Label + "_egg.txt";
                var name = ShowdownParsing.GetShowdownText(pk).Split(new char[] { '\n' }, StringSplitOptions.RemoveEmptyEntries)[0];

                // Clean
                name = name.Replace("(M)", string.Empty).Replace("(F)", string.Empty).Replace("Egg", string.Empty).Replace("(", string.Empty).Replace(")", string.Empty).TrimStart().TrimEnd();

                var text = $"Desired: {name}\r\nAttempt: {numAttempts}";

                File.WriteAllText(fileName, text);
            }
            catch (Exception e)
            {
                LogUtil.LogError($"Failed to write egg file: {e.Message}", nameof(FancyEggBot));
            }
        }
    }
}
