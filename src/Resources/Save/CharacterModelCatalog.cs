using System;
using System.Collections.Generic;

namespace QFG5Extractor.Resources.Save
{
    public sealed class CharacterModelInfo
    {
        public int Id { get; private set; }
        public string Name { get; private set; }

        public CharacterModelInfo(int id, string name)
        {
            Id = id;
            Name = name;
        }

        public override string ToString()
        {
            return string.Format("{0} - {1}", Id, Name);
        }
    }

    public static class CharacterModelCatalog
    {
        private static readonly CharacterModelInfo[] models =
        {
            new CharacterModelInfo(10, "Hero"),
            new CharacterModelInfo(20, "Elsa"),
            new CharacterModelInfo(21, "Giant Mutant Rocketship?"),
            new CharacterModelInfo(25, "Magnum"),
            new CharacterModelInfo(26, "Miniature Mutant Evil Flying Pod?"),
            new CharacterModelInfo(30, "Kokeeno Pookameeso"),
            new CharacterModelInfo(31, "Bruno"),
            new CharacterModelInfo(32, "Gort"),
            new CharacterModelInfo(33, "Minos"),
            new CharacterModelInfo(34, "Katrina"),
            new CharacterModelInfo(35, "Erana"),
            new CharacterModelInfo(36, "Sarra"),
            new CharacterModelInfo(37, "Marrak"),
            new CharacterModelInfo(38, "Salla"),
            new CharacterModelInfo(39, "Wolfie"),
            new CharacterModelInfo(40, "Pholus"),
            new CharacterModelInfo(41, "Logos"),
            new CharacterModelInfo(42, "Fat Fighter #1"),
            new CharacterModelInfo(43, "Fat Fighter #2"),
            new CharacterModelInfo(44, "Fat Fighter #3"),
            new CharacterModelInfo(45, "Fat Fighter #4"),
            new CharacterModelInfo(46, "Ferrari"),
            new CharacterModelInfo(47, "Ugarte"),
            new CharacterModelInfo(48, "Budar"),
            new CharacterModelInfo(49, "Nawar"),
            new CharacterModelInfo(50, "Da F. A."),
            new CharacterModelInfo(51, "Arestes"),
            new CharacterModelInfo(52, "Ann"),
            new CharacterModelInfo(53, "Salim"),
            new CharacterModelInfo(54, "Julanar"),
            new CharacterModelInfo(55, "Rakeesh"),
            new CharacterModelInfo(56, "Shakra"),
            new CharacterModelInfo(57, "Sam"),
            new CharacterModelInfo(58, "Toro"),
            new CharacterModelInfo(59, "Andre"),
            new CharacterModelInfo(60, "Light Mentality Scientist Dude"),
            new CharacterModelInfo(61, "Dark Mentality Scientist Dude"),
            new CharacterModelInfo(62, "Erasmus"),
            new CharacterModelInfo(63, "Fenr[is/us]"),
            new CharacterModelInfo(64, "Sybil"),
            new CharacterModelInfo(65, "Hades"),
            new CharacterModelInfo(66, "Queen"),
            new CharacterModelInfo(67, "Cerberus"),
            new CharacterModelInfo(69, "Guard"),
            new CharacterModelInfo(70, "Townsman #1"),
            new CharacterModelInfo(71, "Townsman #2"),
            new CharacterModelInfo(72, "Townsman #3"),
            new CharacterModelInfo(73, "Townsman #4"),
            new CharacterModelInfo(74, "Invaders/Mercanaries"),
            new CharacterModelInfo(75, "General"),
            new CharacterModelInfo(76, "Wizard"),
            new CharacterModelInfo(77, "Hydra"),
            new CharacterModelInfo(78, "War Minotaur"),
            new CharacterModelInfo(79, "Triton"),
            new CharacterModelInfo(80, "Salamander"),
            new CharacterModelInfo(81, "Dragonfish"),
            new CharacterModelInfo(82, "Remora"),
            new CharacterModelInfo(83, "Shade"),
            new CharacterModelInfo(84, "Lemur"),
            new CharacterModelInfo(85, "Manes"),
            new CharacterModelInfo(86, "Goon #1"),
            new CharacterModelInfo(87, "Goon #2"),
            new CharacterModelInfo(88, "Goon #3"),
            new CharacterModelInfo(89, "Boarman #1"),
            new CharacterModelInfo(90, "Bearman #2"),
            new CharacterModelInfo(91, "Cougarman #3"),
            new CharacterModelInfo(92, "Boarman #1"),
            new CharacterModelInfo(93, "Bearman #2"),
            new CharacterModelInfo(94, "Cougarman #3"),
            new CharacterModelInfo(95, "Homunculous"),
            new CharacterModelInfo(96, "Battie"),
            new CharacterModelInfo(97, "Grangler"),
            new CharacterModelInfo(98, "Dryad Spirit"),
            new CharacterModelInfo(100, "Dragon"),
            new CharacterModelInfo(102, "Weirdling"),
            new CharacterModelInfo(103, "Townswoman #1"),
            new CharacterModelInfo(104, "Townswoman #2"),
            new CharacterModelInfo(105, "Townswoman #3"),
            new CharacterModelInfo(106, "Fish #1"),
            new CharacterModelInfo(107, "Fish #2"),
            new CharacterModelInfo(108, "Fish #3"),
            new CharacterModelInfo(109, "Fish #4"),
            new CharacterModelInfo(110, "Pegasus"),
            new CharacterModelInfo(113, "Nawar-with-Hero's-Head"),
            new CharacterModelInfo(114, "Dragonling"),
            new CharacterModelInfo(115, "Icarus Wings"),
            new CharacterModelInfo(116, "Walking Table"),
            new CharacterModelInfo(117, "Chair #1"),
            new CharacterModelInfo(118, "Chair #2"),
            new CharacterModelInfo(119, "Table"),
            new CharacterModelInfo(120, "Chair #3"),
            new CharacterModelInfo(121, "Horn? Spike? Claw?"),
            new CharacterModelInfo(122, "Popcorn Machine"),
            new CharacterModelInfo(123, "Urn"),
            new CharacterModelInfo(124, "Tea Set"),
            new CharacterModelInfo(125, "Giant Swiss Cheese Donut"),
            new CharacterModelInfo(126, "Canoe"),
            new CharacterModelInfo(127, "Sailboat"),
            new CharacterModelInfo(128, "Balloon"),
            new CharacterModelInfo(129, "Dragon Pillar"),
            new CharacterModelInfo(130, "Broken Dragon Pillar"),
            new CharacterModelInfo(131, "Chest"),
            new CharacterModelInfo(132, "Chair"),
            new CharacterModelInfo(133, "Hero-with-Wings"),
            new CharacterModelInfo(134, "Bee Hive #1"),
            new CharacterModelInfo(135, "Bee Hive #2"),
            new CharacterModelInfo(136, "Dead General"),
            new CharacterModelInfo(137, "Boulder")
        };

        private static readonly IList<CharacterModelInfo> readOnlyModels =
            Array.AsReadOnly(models);

        public static IList<CharacterModelInfo> Models
        {
            get { return readOnlyModels; }
        }

        public static string GetName(int id)
        {
            foreach (CharacterModelInfo model in models)
            {
                if (model.Id == id) return model.Name;
            }
            return null;
        }
    }
}
