using PKHeX.Core;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace SysBot.Pokemon
{
    public class BreedingLegality
    {
        private const int Everstone = 229;

        private static readonly Dictionary<int[], int> BabyHeldItems = new Dictionary<int[], int>()
        {
            { new[] { 183, 184 }, 254 }, // Azurill
            { new[] { 202 }, 255 }, // Wynaut
            { new[] { 315, 407 }, 318 }, // Budew
            { new[] { 143 }, 316 }, // Munchlax
            { new[] { 113, 242 }, 319 }, // Happiny
            { new[] { 185 }, 315 }, // Bonsly
            { new[] { 122, 866 }, 314 }, // Mime Jr
            { new[] { 458 }, 317 }, // Mantyke
            { new[] { 358 }, 320 }, // Chimecho
        };

        private static readonly List<int> NoEggGroup = new List<int>()
        {
            30, 31, 144, 145, 146, 150, 151,
            172, 173, 174, 175, 201, 236, 238, 239, 240, 243, 244, 245, 249, 250, 251,
            298, 350, 377, 378, 379, 380, 381, 382, 383, 384, 385, 386,
            406, 433, 438, 439, 440, 446, 447, 458, 480, 481, 482, 483, 484, 485, 486, 487, 488, 491, 492, 493, 494,
            638, 639, 640, 641, 642, 643, 644, 645, 646, 647, 648, 649,
            716, 717, 718, 719, 720, 721,
            772, 773, 785, 786, 787, 788, 789, 790, 791, 792, 793, 794, 795, 796, 797, 798, 799, 800, 801, 802, 803, 804, 805, 806, 807, 808, 809,
            848, 880, 881, 882, 883, 888, 889, 890, 891, 892, 893, 894, 895, 896, 897, 898, 905,

            132 // ditto can't breed with itself
        };

        public static bool CanBreed(int pkm, int form)
        {
            if (NoEggGroup.Contains(pkm))
                return false;
            switch (pkm)
            {
                case 25 when form != 0: // Pikachu caps
                case 658 when form != 0: // Greninja, possibly deleted from Pokemon universe
                    return false;
            }
            return true;
        }

        public static void EnsureCorrectHeldItem<T>(T pk) where T : PKM, new()
        {
            var kvp = BabyHeldItems.Where(e => e.Key.Contains(pk.Species));
            if (kvp.Any())
                pk.HeldItem = kvp.First().Value;
            else
                pk.HeldItem = Everstone;
        }
    }
}
