using System;

namespace RealLifeOverhaul
{
    /// <summary>What home and health ask of the rest of the mod (Wiring/LifeLinks fills each in, and Wiring/MealsLinks <see cref="ServedAtTable"/>).</summary>
    public static class HomeBridges
    {
        /// <summary>
        /// Whether the player is under a roof the game does not know of: up in a hotel room, where the player stands at the
        /// hotel's door in the street while the room's list is open. Neither rain nor the street reaches them there.
        /// Default: no.
        /// </summary>
        public static Func<bool> Sheltered;

        /// <summary>
        /// The quality of the bed (0 to 100) the player sleeps in away from the game's own beds, such as a hotel room's
        /// through its time skip, or -1 when not. While it is 0 or more the hours count as sleep. Default: never.
        /// </summary>
        public static Func<int> SleepingAway;

        /// <summary>Booked quality for an actual native guest bed; -1 leaves the native bed's quality unchanged.</summary>
        public static Func<int> PhysicalBedQuality;

        /// <summary>A private guest room is indoors but has no public audience. Default: false.</summary>
        public static Func<bool> PrivateGuestRoom;

        /// <summary>
        /// A dish cooked at home is ready and about to be eaten (its fill, of the game's 100): true when it is carried to a
        /// free seat and eaten there, so the cooker's bite is left out. The meal's effect is the cooking's own either way.
        /// Default: eaten at the cooker.
        /// </summary>
        public static Func<int, bool> ServedAtTable;
    }
}
