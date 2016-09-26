using System.Collections.Generic;

namespace EvFutBot.Utilities
{
    public class League
    {
        public string AbbrName { get; set; }
        public int Id { get; set; }
        public object ImgUrl { get; set; }
        public string Name { get; set; }
    }

    public class ImageUrls
    {
        public string Small { get; set; }
        public string Medium { get; set; }
        public string Large { get; set; }
    }

    public class Nation
    {
        public ImageUrls ImageUrls { get; set; }
        public string AbbrName { get; set; }
        public int Id { get; set; }
        public object ImgUrl { get; set; }
        public string Name { get; set; }
    }

    public class Dark
    {
        public string Small { get; set; }
        public string Medium { get; set; }
        public string Large { get; set; }
    }

    public class Normal
    {
        public string Small { get; set; }
        public string Medium { get; set; }
        public string Large { get; set; }
    }

    public class ImageUrls2
    {
        public Dark Dark { get; set; }
        public Normal Normal { get; set; }
    }

    public class Club
    {
        public ImageUrls2 ImageUrls { get; set; }
        public string AbbrName { get; set; }
        public int Id { get; set; }
        public object ImgUrl { get; set; }
        public string Name { get; set; }
    }

    public class Headshot
    {
        public string LargeImgUrl { get; set; }
        public string MedImgUrl { get; set; }
        public string SmallImgUrl { get; set; }
    }

    public class SpecialImages
    {
        public string LargeTotwImgUrl { get; set; }
        public string MedTotwImgUrl { get; set; }
    }

    public class Attribute
    {
        public string Name { get; set; }
        public int Value { get; set; }
        public List<int> ChemistryBonus { get; set; }
    }

    public class Item
    {
        public string CommonName { get; set; }
        public string FirstName { get; set; }
        public string HeadshotImgUrl { get; set; }
        public string LastName { get; set; }
        public League League { get; set; }
        public Nation Nation { get; set; }
        public Club Club { get; set; }
        public Headshot Headshot { get; set; }
        public SpecialImages SpecialImages { get; set; }
        public string Position { get; set; }
        public string PlayStyle { get; set; }
        public object PlayStyleId { get; set; }
        public int Height { get; set; }
        public int Weight { get; set; }
        public string Birthdate { get; set; }
        public int Age { get; set; }
        public int Acceleration { get; set; }
        public int Aggression { get; set; }
        public int Agility { get; set; }
        public int Balance { get; set; }
        public int Ballcontrol { get; set; }
        public string Foot { get; set; }
        public int SkillMoves { get; set; }
        public int Crossing { get; set; }
        public int Curve { get; set; }
        public int Dribbling { get; set; }
        public int Finishing { get; set; }
        public int Freekickaccuracy { get; set; }
        public int Gkdiving { get; set; }
        public int Gkhandling { get; set; }
        public int Gkkicking { get; set; }
        public int Gkpositioning { get; set; }
        public int Gkreflexes { get; set; }
        public int Headingaccuracy { get; set; }
        public int Interceptions { get; set; }
        public int Jumping { get; set; }
        public int Longpassing { get; set; }
        public int Longshots { get; set; }
        public int Marking { get; set; }
        public int Penalties { get; set; }
        public int Positioning { get; set; }
        public int Potential { get; set; }
        public int Reactions { get; set; }
        public int Shortpassing { get; set; }
        public int Shotpower { get; set; }
        public int Slidingtackle { get; set; }
        public int Sprintspeed { get; set; }
        public int Standingtackle { get; set; }
        public int Stamina { get; set; }
        public int Strength { get; set; }
        public int Vision { get; set; }
        public int Volleys { get; set; }
        public int WeakFoot { get; set; }
        public List<string> Traits { get; set; }
        public List<string> Specialities { get; set; }
        public string AtkWorkRate { get; set; }
        public string DefWorkRate { get; set; }
        public string PlayerType { get; set; }
        public List<Attribute> Attributes { get; set; }
        public string Name { get; set; }
        public string Quality { get; set; }
        public string Color { get; set; }
        public bool IsGk { get; set; }
        public string PositionFull { get; set; }
        public bool IsSpecialType { get; set; }
        public object Contracts { get; set; }
        public object Fitness { get; set; }
        public object RawAttributeChemistryBonus { get; set; }
        public object IsLoan { get; set; }
        public string ItemType { get; set; }
        public object DiscardValue { get; set; }
        public string Id { get; set; }
        public string ModelName { get; set; }
        public int BaseId { get; set; }
        public int Rating { get; set; }
    }

    public class PlayersSearchRequest
    {
        public int Page { get; set; }
        public int TotalPages { get; set; }
        public int TotalResults { get; set; }
        public string Type { get; set; }
        public int Count { get; set; }
        public List<Item> Items { get; set; }
    }
}