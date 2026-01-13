using System;
using System.Collections.Generic;

namespace YARG.Core.Game
{
    [Flags]
    public enum Modifier : ulong
    {
        None          = 0,
        AllStrums     = 1 << 0,
        AllHopos      = 1 << 1,
        AllTaps       = 1 << 2,
        HoposToTaps   = 1 << 3,
        TapsToHopos   = 1 << 4,
        NoteShuffle   = 1 << 5,
        NoKicks       = 1 << 6,
        UnpitchedOnly = 1 << 7,
        NoDynamics    = 1 << 8,
        NoVocalPercussion = 1 << 9,
        RangeCompress = 1 << 10,
        NoOpens = 1 << 11
    }

    public static class ModifierConflicts
    {
        // We can essentially treat a set of conflicting modifiers as a group, since they
        // conflict in both ways (i.e. all strums conflicts with all HOPOs, and vice versa).
        // Returning a list of the conflicting modifiers, and simply removing them, takes
        // care of all of the possibilities. A modifier can be a part of multiple groups,
        // which is why we use a list here.
        private static readonly List<Modifier> _conflictingModifiers = new()
        {
            Modifier.AllStrums   |
            Modifier.AllHopos    |
            Modifier.AllTaps     |
            Modifier.HoposToTaps |
            Modifier.TapsToHopos,
        };

        // Returns two modifier sets. The first set ("possible" modifiers) represents modifiers that should be
        // selectable for this combination of GameMode and Instrument. The second ("excusable" modifiers) are
        // those that should not be selectable for this combination, but should be selectable for the same GameMode
        // with a different Instrument. Excusable modifiers are not listed in the modifier menu, but are also not
        // cleared behind the scenes, unlike the "impossible" modifiers that are not captured in either returned set.
        public static (Modifier possible, Modifier excusable) PossibleModifiers(this GameMode gameMode, Instrument instrument)
        {
            var all = gameMode.AllModifiers();

            var excusable = instrument switch {
                Instrument.ProKeys =>
                    Modifier.RangeCompress,

                _ => Modifier.None
            };

            var possible = all & ~excusable;

            return (possible, excusable);
        }

        private static Modifier AllModifiers(this GameMode gameMode)
        {
            return gameMode switch
            {
                GameMode.FiveFretGuitar =>
                    Modifier.AllStrums     |
                    Modifier.AllHopos      |
                    Modifier.AllTaps       |
                    Modifier.HoposToTaps   |
                    Modifier.TapsToHopos   |
                    Modifier.RangeCompress |
                    Modifier.NoOpens,

                GameMode.FourLaneDrums or
                GameMode.FiveLaneDrums or
                GameMode.EliteDrums =>
                    Modifier.NoKicks    |
                    Modifier.NoDynamics,

                GameMode.Vocals =>
                    Modifier.UnpitchedOnly |
                    Modifier.NoVocalPercussion,

                GameMode.ProKeys =>
                    Modifier.RangeCompress,

                GameMode.SixFretGuitar or
                GameMode.ProGuitar     or
            //  GameMode.Dj            or
                GameMode.ProKeys       => Modifier.None,

                _  => throw new NotImplementedException($"Unhandled game mode {gameMode}!")
            };
        }

        public static Modifier FromSingleModifier(Modifier modifier)
        {
            var output = Modifier.None;

            foreach (var conflictSet in _conflictingModifiers)
            {
                if ((conflictSet & modifier) == 0) continue;

                // Set conflicts
                output |= conflictSet;

                // Make sure to get rid of the modifier itself
                output &= ~modifier;
            }

            return output;
        }
    }
}