namespace RealLifeOverhaul
{
    /// <summary>Which stays have a proved native room. Unsupported categories and shared worlds use the menu room.</summary>
    public static class HotelVisitRules
    {
        public static bool Supported(string room, string size, int version, bool multiplayer)
        {
            return !multiplayer && room == "standard" && size == "ba:buildingsize_l" && version == 1;
        }

        /// <summary>A rental-market flag alone says nothing about occupancy; saved items or another business do.</summary>
        public static bool Empty(bool rented, bool owned, bool business, string layout, string type, int items)
        {
            return !rented && !owned && !business && string.IsNullOrEmpty(layout) && type == "ba:businesstype_empty" && items == 0;
        }
    }
}
