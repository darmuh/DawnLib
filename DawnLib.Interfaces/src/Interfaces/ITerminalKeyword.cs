using InjectionLibrary.Attributes;

[assembly: RequiresInjections]

namespace Dawn;

[InjectInterface(typeof(TerminalKeyword))]
public interface ITerminalKeyword
{
    public enum KeywordType
    {
        Unset = 0, //used for default value before it has been set, ie. vanilla keywords or keywords added outside of DawnLib
        VanillaCore, //core vanilla keywords that overwriting could severely affect gameplay
        Code, //doors, turrets, etc.
        Moon, 
        VehicleItem, 
        ShopItem,
        BestiaryItem,
        StoryLogItem,
        Command, //Standard DawnLib-made commands
        Other
    }

    //used to resolve conflicts between keywords of same word
    public KeywordType KeywordPriority { get; set; }

    //Determines if keyword should load it's result if additional text is detected after the keyword
    public bool AcceptAdditionalText { get; set; }

    //Placeholder value, can be used to describe what a command does (modded/vanilla)
    public string Description { get; set; }

    //Placeholder value, can be used in the future to categorize commands (modded/vanilla)
    public string Category { get; set; }

    //Both Category/Description could be used in an interactive style menu listing all possible keywords that can be run from the terminal
}
