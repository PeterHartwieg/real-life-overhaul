namespace RealLifeOverhaul.ModApi
{
    /// <summary>An immutable Flare identity for other mods. Prepared once when the city's population changes.</summary>
    public sealed class CityApiCitizen
    {
        public readonly int Index;
        public readonly string First, Last;
        public readonly string District;
        public readonly int Age;
        public readonly bool Female;
        public readonly string Persona;
        public readonly int AppearanceSeed;

        internal CityApiCitizen(ModApiIdentity identity)
        {
            Index = identity.Index;
            First = identity.First;
            Last = identity.Last;
            District = identity.District;
            Age = identity.Age;
            Female = identity.Female;
            Persona = ModApiRules.Persona(identity.Persona);
            AppearanceSeed = identity.AppearanceSeed;
        }
    }
}
