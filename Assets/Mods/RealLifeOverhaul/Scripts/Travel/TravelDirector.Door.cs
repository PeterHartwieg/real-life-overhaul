using System;
using Helpers;
using RealLifeOverhaul.GameUi;
using UI.Notification;
using UnityEngine;

namespace RealLifeOverhaul
{
    /// <summary>The game's panel at a hotel's or a travel agency's door, and its buttons.</summary>
    public sealed partial class TravelDirector
    {
        private const string ButtonReception = "reallife_travel_button_reception";
        private const string ButtonRoom = "reallife_travel_button_room";
        private const string ButtonAgent = "reallife_travel_button_agent";
        private const float DoorReachUp = 2.2f;     // metres over the door the game's panel stands

        private int _doorKind;              // what the panel says: 1 reception, 2 reception and the room, 3 the agent
        private HotelSite _doorSite;
        private int _doorDay = -1;
        private bool _doorRoom;
        private Action _onReception;
        private Action _onRoom;
        private Action _onAgent;
        private Func<string> _doorWhat;

        // Once a second: the panel over the door the player stands at, with what is done there on the game's own keys.
        private void UpdatePrompt(GameManager gm)
        {
            int kind = 0;
            HotelSite site = null;
            if (!_secluded && !JobDirector.TakesInteractKey && !ThirdPersonCamera.IsUiOpen() && !gm.playerController.NavigationDisabled
                && !BuildingManager.IsInsideBuilding && DialogController.current == null)
            {
                site = Hotels.InReach;
                if (site != null && Settings.Hotels)
                {
                    kind = TravelState.HasRoom && TravelState.HotelId == site.Slot.Id ? 2 : 1;
                }
                else if (Holidays.AgencyInReach != null && Settings.Holidays)
                {
                    kind = 3;
                    site = null;
                }
            }
            if (kind == 0)
            {
                HidePrompt(Window.IsOpen ? "a list is open" : "the player walked away or another screen is open");
                return;
            }
            int day = TimeHelper.CurrentDay;
            bool room = TravelState.HasRoom;
            if (kind != _doorKind || !ReferenceEquals(site, _doorSite) || day != _doorDay || room != _doorRoom)
            {
                _doorKind = kind;
                _doorSite = site;
                _doorDay = day;
                _doorRoom = room;
                WriteDoor(site, kind);
            }
            Vector3 at = gm.playerController.transform.position;
            Vector3 place = kind == 3 ? Holidays.AgencyDoor : site.Desk;
            _door.Show(place, at, DoorReachUp, _doorWhat);
        }

        // For the log line, built only when the game accepts or refuses the panel.
        private string DoorWhat()
        {
            if (_doorKind == 3)
            {
                return "at a travel agency's door";
            }
            return "at " + (_doorSite != null ? _doorSite.Name : "a hotel") + " ("
                   + (_doorKind == 2 ? "a room is booked here" : _doorRoom ? "a room is booked elsewhere" : "no room booked") + ")";
        }

        // Reception on the interact key, unless a room is held; the room on the secondary key.
        private void WriteDoor(HotelSite site, int kind)
        {
            WorldPanel panel = _door.Panel;
            if (kind == 3)
            {
                panel.Lines(TravelText.T("reallife_travel_door_agency"), TravelText.T("reallife_travel_door_agency_line"), null, WorldPanel.White());
                panel.Buttons(ButtonAgent, _onAgent, true, null, null, false);
                return;
            }
            string left;
            if (kind == 2)
            {
                left = TravelText.T("reallife_travel_door_yours", "room", TravelText.T(TravelCatalogue.Room(TravelState.RoomId).NameKey));
            }
            else if (TravelState.HasRoom)
            {
                left = TravelText.T("reallife_travel_door_elsewhere", "hotel", TravelText.T(TravelCatalogue.Hotel(TravelState.HotelId).NameKey));
            }
            else
            {
                RoomType[] rooms = TravelCatalogue.Rooms(site.Slot.Tier);
                int from = int.MaxValue;
                for (int i = 0; i < rooms.Length; i++)
                {
                    from = Mathf.Min(from, HotelStay.Tonight(site.Slot, rooms[i]));
                }
                left = TravelText.T("reallife_travel_door_from", "rate", TravelText.Money(from));
            }
            panel.Lines(site.Name, left, null, WorldPanel.White());
            panel.Button(0, ButtonReception, _onReception, !TravelState.HasRoom, true);
            panel.Button(1, kind == 2 ? ButtonRoom : null, kind == 2 ? _onRoom : null, kind == 2, false);
            panel.Button(2, null, null, false, false);
            panel.Button(3, null, null, false, false);
        }

        private void HidePrompt(string why)
        {
            if (_door.Up)
            {
                _doorKind = 0;
                _doorSite = null;
                _door.Hide(why);
            }
        }

        // Whether the player is still free to be served when a button runs.
        private bool Free(out GameManager gm)
        {
            gm = InstanceBehavior<GameManager>.Instance;
            return gm != null && gm.playerController != null && gm.selectedVehicle == null && !PlayerHelper.IsUsingVehicle
                   && !gm.playerController.NavigationDisabled && !ThirdPersonCamera.IsUiOpen() && DialogController.current == null;
        }

        // The buttons run from the game's shared panel: a fault stays here rather than reaching the panel's own guard.
        private void ReceptionClicked()
        {
            try
            {
                HotelSite site = Hotels.InReach;
                if (!Free(out GameManager gm) || site == null)
                {
                    return;
                }
                HidePrompt("reception was chosen");
                gm.playerController.ResetNavigation();
                Stay.OpenDesk(site);
            }
            catch (Exception e)
            {
                Fault(e);
            }
        }

        private void RoomClicked()
        {
            try
            {
                if (!Free(out GameManager gm) || Hotels.InReach == null)
                {
                    return;
                }
                HidePrompt("the room was chosen");
                gm.playerController.ResetNavigation();
                if (!Stay.EnterRoom(out TestRefusal why, Hotels.InReach) && why != TestRefusal.None && why != TestRefusal.NothingFits)
                {
                    TravelText.Toast(NotificationType.Info, TravelText.T("reallife_travel_room_not_now"), 5f);
                }
            }
            catch (Exception e)
            {
                Fault(e);
            }
        }

        private void AgentClicked()
        {
            try
            {
                if (!Free(out GameManager gm) || Holidays.AgencyInReach == null)
                {
                    return;
                }
                BuildingRegistration agency = Holidays.AgencyInReach;
                HidePrompt("the agent was chosen");
                gm.playerController.ResetNavigation();
                Holidays.OpenAgent(agency);
            }
            catch (Exception e)
            {
                Fault(e);
            }
        }
    }
}
