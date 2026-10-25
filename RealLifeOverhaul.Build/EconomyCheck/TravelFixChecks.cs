using System;

// Faults found in Scripts/Travel: going up to a hotel room was refused while the game was paused, because the room asked
// the life events' test for a quiet moment, which counts a paused clock as busy.
internal static partial class Program
{
    private static void TravelFixes()
    {
        string life = ModSource(@"Life\LifeEvents.cs");
        Check(life.Contains("public static bool IsSafeForEvent(bool pausedIsFine = false)") && life.Contains("&& (pausedIsFine || !uis.gameSpeed.Paused);"),
            "travel fix: the life events' quiet moment counts a paused clock as busy unless the caller says a pause is fine");
        string director = ModSource(@"Travel\TravelDirector.cs");
        Check(director.Contains("public bool CanSeclude(out TestRefusal why, bool pausedIsFine = false)")
              && director.Contains("!LifeEvents.IsSafeForEvent(pausedIsFine)"),
            "travel fix: being held in place passes the caller's word on a pause to the quiet moment");
        string stay = ModSource(@"Travel\HotelStay.cs");
        Check(MemberText(stay, "public bool EnterRoom(out TestRefusal why, HotelSite site = null, bool physical = true)")
              .Contains("_director.CanSeclude(out why, pausedIsFine: true)"),
            "travel fix: going up to the room is allowed while the game is paused");
        Check(FixLineEverywhere("go up to your hotel room while the game is paused", "subir para o seu quarto de hotel com o jogo em pausa",
                                "游戏暂停时也能上楼回酒店房间", "auch bei pausiertem Spiel auf dein Hotelzimmer", "monter dans votre chambre d'hôtel pendant que le jeu est en pause",
                                "subir a tu habitación de hotel con el juego en pausa", "подняться в номер отеля, пока игра на паузе",
                                "naar je hotelkamer gaan terwijl het spel is gepauzeerd", "oyun duraklatılmışken artık otel odanıza çıkabilirsiniz"),
            "travel fix: What's new has the paused room's line, with its fix mark, in some row of every language");
    }
}
