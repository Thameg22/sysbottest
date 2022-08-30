using PKHeX.Core;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace SysBot.Pokemon
{
    public class DayCareStructure
    {
        public bool Slot1Occupied { get; private set; }
        public PK8 Slot1 { get; private set; }
        public bool Slot2Occupied { get; private set; }
        public PK8 Slot2 { get; private set; }

        public const int DAYCARE_MAIN_SIZE = 2 + (0x148 * 2);

        public DayCareStructure(byte[] data)
        {
            if (data.Length != DAYCARE_MAIN_SIZE)
                throw new Exception("Daycare data is of an invalid size");
            Slot1Occupied = data[0] == 1;
            Slot1 = new PK8(data.Skip(1).Take(0x148).ToArray());
            Slot2Occupied = data[0x149] == 1;
            Slot2 = new PK8(data.Skip(0x149).Take(0x148).ToArray());
        }

        public void OccupySlot(int slot, PK8 pk)
        {
            if (slot == 0)
            {
                Slot1Occupied = true;
                Slot1 = pk;
            }
            else
            {
                Slot2Occupied = true;
                Slot2 = pk;
            }
        }

        public byte[] GetData()
        {
            var bytes = new List<byte>();
            bytes.Add((byte)(Slot1Occupied ? 1 : 0));
            bytes.AddRange(Slot1.EncryptedBoxData);
            bytes.Add((byte)(Slot2Occupied ? 1 : 0));
            bytes.AddRange(Slot2.EncryptedBoxData);
            return bytes.ToArray();
        }

        public string GetSummary()
        {
            StringBuilder sb = new StringBuilder();
            sb.AppendLine($"Daycare Slot 1 is {(Slot1Occupied ? string.Empty : "not ")}occupied");
            if (Slot1Occupied)
                sb.AppendLine(ShowdownParsing.GetShowdownText(Slot1));
            sb.AppendLine($"Daycare Slot 2 is {(Slot2Occupied ? string.Empty : "not ")}occupied");
            if (Slot2Occupied)
                sb.AppendLine(ShowdownParsing.GetShowdownText(Slot2));
            return sb.ToString();
        }
    }
}
