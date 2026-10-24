using System;
using System.Collections.Generic;

namespace QFG5Extractor.Resources.Panorama
{
    public sealed class RoomLocationInfo
    {
        public int Id { get; private set; }
        public string Name { get; private set; }

        public RoomLocationInfo(int id, string name)
        {
            Id = id;
            Name = name;
        }

        public override string ToString()
        {
            return string.Format("{0} - {1}", Id, Name);
        }
    }

    public static class RoomLocationCatalog
    {
        private static readonly RoomLocationInfo[] locations =
        {
            new RoomLocationInfo(200, "Town Square"),
            new RoomLocationInfo(210, "Docks"),
            new RoomLocationInfo(220, "Nob Hill"),
            new RoomLocationInfo(230, "Low Town"),
            new RoomLocationInfo(240, "Hall of Kings"),
            new RoomLocationInfo(250, "Dead Parrot Inn and Casino"),
            new RoomLocationInfo(260, "Bedroom for Multiplayer Game"),
            new RoomLocationInfo(270, "Famous Adventurer's Correspondence School"),
            new RoomLocationInfo(280, "Thief's Guild"),
            new RoomLocationInfo(290, "Gnome Ann's Land Inn"),
            new RoomLocationInfo(300, "Bedroom"),
            new RoomLocationInfo(310, "Apothecary"),
            new RoomLocationInfo(320, "Magic Shop"),
            new RoomLocationInfo(340, "Break-in Bank"),
            new RoomLocationInfo(350, "Arena Antechamber"),
            new RoomLocationInfo(355, "Arena Grounds"),
            new RoomLocationInfo(360, "Break-in House"),
            new RoomLocationInfo(370, "Ferrari's House"),
            new RoomLocationInfo(380, "Adventurer's Guild"),
            new RoomLocationInfo(390, "Science Island Bridge Beach"),
            new RoomLocationInfo(400, "Science Island Main Room and Secret Lab"),
            new RoomLocationInfo(410, "Arcane Isle Establishing Shot"),
            new RoomLocationInfo(415, "Cloud Entrance to Arcane Isle"),
            new RoomLocationInfo(420, "Erasmus' House"),
            new RoomLocationInfo(440, "Dock Entrance to Atlantis"),
            new RoomLocationInfo(450, "Atlantis Main Entrance Area"),
            new RoomLocationInfo(460, "Temple of Poseidon"),
            new RoomLocationInfo(470, "Dryad Woods"),
            new RoomLocationInfo(480, "Oracle Room"),
            new RoomLocationInfo(500, "Minos Main Entrance"),
            new RoomLocationInfo(510, "Minos Hallway"),
            new RoomLocationInfo(520, "Minos Treasure Room"),
            new RoomLocationInfo(530, "Hydra Cave Entrance"),
            new RoomLocationInfo(540, "Hydra Interior Cave"),
            new RoomLocationInfo(550, "Island Entrance to Hades (after)"),
            new RoomLocationInfo(551, "Island Entrance to Hades (before)"),
            new RoomLocationInfo(560, "Hades Skeleton Room"),
            new RoomLocationInfo(565, "Hades Cave Room"),
            new RoomLocationInfo(570, "Hades Main Hall, Rivers Lethe and Styx"),
            new RoomLocationInfo(580, "Tinos"),
            new RoomLocationInfo(581, "Tinos Interior 1"),
            new RoomLocationInfo(582, "Tinos Interior 2"),
            new RoomLocationInfo(583, "Tinos Interior 3"),
            new RoomLocationInfo(584, "Tinos Interior 4"),
            new RoomLocationInfo(585, "Tinos Interior 5"),
            new RoomLocationInfo(586, "Tinos Interior 6"),
            new RoomLocationInfo(587, "Tinos Interior 7"),
            new RoomLocationInfo(588, "Tinos Interior 8"),
            new RoomLocationInfo(589, "Tinos Interior 9"),
            new RoomLocationInfo(631, "Tinos Interior 10"),
            new RoomLocationInfo(632, "Tinos Interior 11"),
            new RoomLocationInfo(590, "Paros"),
            new RoomLocationInfo(591, "Paros Interior 1"),
            new RoomLocationInfo(592, "Paros Interior 2"),
            new RoomLocationInfo(593, "Paros Interior 3"),
            new RoomLocationInfo(594, "Paros Interior 4"),
            new RoomLocationInfo(595, "Paros Interior 5"),
            new RoomLocationInfo(596, "Paros Interior 6"),
            new RoomLocationInfo(597, "Paros Interior 7"),
            new RoomLocationInfo(600, "Naxos"),
            new RoomLocationInfo(601, "Naxos Interior 1"),
            new RoomLocationInfo(602, "Naxos Interior 2"),
            new RoomLocationInfo(603, "Naxos Interior 3"),
            new RoomLocationInfo(604, "Naxos Interior 4"),
            new RoomLocationInfo(605, "Naxos Interior 5"),
            new RoomLocationInfo(606, "Naxos Interior 6"),
            new RoomLocationInfo(607, "Naxos Interior 7"),
            new RoomLocationInfo(608, "Naxos Interior 8"),
            new RoomLocationInfo(609, "Naxos Interior 9"),
            new RoomLocationInfo(633, "Naxos Interior 10"),
            new RoomLocationInfo(634, "Naxos Interior 11"),
            new RoomLocationInfo(610, "Keros"),
            new RoomLocationInfo(611, "Keros Interior 1"),
            new RoomLocationInfo(612, "Keros Interior 2"),
            new RoomLocationInfo(613, "Keros Interior 3"),
            new RoomLocationInfo(614, "Keros Interior 4"),
            new RoomLocationInfo(615, "Keros Interior 5"),
            new RoomLocationInfo(616, "Keros Interior 6"),
            new RoomLocationInfo(617, "Keros Interior 7"),
            new RoomLocationInfo(618, "Keros Interior 8"),
            new RoomLocationInfo(620, "Ios"),
            new RoomLocationInfo(621, "Ios Interior 1"),
            new RoomLocationInfo(622, "Ios Interior 2"),
            new RoomLocationInfo(623, "Ios Interior 3"),
            new RoomLocationInfo(624, "Ios Interior 4"),
            new RoomLocationInfo(625, "Ios Interior 5"),
            new RoomLocationInfo(626, "Ios Interior 6"),
            new RoomLocationInfo(627, "Ios Interior 7"),
            new RoomLocationInfo(628, "Ios Interior 8"),
            new RoomLocationInfo(629, "Ios Interior 9"),
            new RoomLocationInfo(680, "Sifnos Exterior - Night"),
            new RoomLocationInfo(681, "Sifnos Exterior - Day"),
            new RoomLocationInfo(690, "Sifnos Interior - Night"),
            new RoomLocationInfo(691, "Sifnos Interior - Day"),
            new RoomLocationInfo(700, "Generic Combat Inland 1"),
            new RoomLocationInfo(701, "Generic Combat Inland 2"),
            new RoomLocationInfo(702, "Generic Combat Inland 3"),
            new RoomLocationInfo(703, "Generic Combat Inland 4"),
            new RoomLocationInfo(704, "Combat Inland East"),
            new RoomLocationInfo(705, "Combat Inland West"),
            new RoomLocationInfo(706, "Combat Inland Far West"),
            new RoomLocationInfo(707, "Combat Inland North"),
            new RoomLocationInfo(720, "Pegasus Nest"),
            new RoomLocationInfo(730, "Dragon Blood Pool"),
            new RoomLocationInfo(735, "Dragon Blood Pool Endgame"),
            new RoomLocationInfo(740, "Dragon Blood Pool Endgame (BIG!)"),
            new RoomLocationInfo(750, "West City Entrance"),
            new RoomLocationInfo(760, "Generic Combat Shoreline 1"),
            new RoomLocationInfo(761, "Generic Combat Shoreline 2"),
            new RoomLocationInfo(762, "Generic Combat at Shoreline 3"),
            new RoomLocationInfo(770, "Erana's Retreat"),
            new RoomLocationInfo(780, "Katrina's Retreat"),
            new RoomLocationInfo(790, "Generic Under the Sea"),
            new RoomLocationInfo(791, "Sea Panorama by Dock"),
            new RoomLocationInfo(795, "Underwater Shoreline"),
            new RoomLocationInfo(900, "Map")
        };

        private static readonly IDictionary<int, RoomLocationInfo> locationsById = CreateLookup();
        private static readonly IList<RoomLocationInfo> readOnlyLocations =
            Array.AsReadOnly(locations);

        public static IList<RoomLocationInfo> Locations
        {
            get { return readOnlyLocations; }
        }

        public static bool TryGet(int id, out RoomLocationInfo location)
        {
            return locationsById.TryGetValue(id, out location);
        }

        private static IDictionary<int, RoomLocationInfo> CreateLookup()
        {
            Dictionary<int, RoomLocationInfo> result = new Dictionary<int, RoomLocationInfo>();
            foreach (RoomLocationInfo location in locations)
                result.Add(location.Id, location);
            return result;
        }
    }
}
