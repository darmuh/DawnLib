using InjectionLibrary.Attributes;

[assembly: RequiresInjections]

namespace Dawn;

[InjectInterface(typeof(TerminalKeyword))]
public interface ITerminalKeyword
{
    //Determines if keyword should load it's result if additional text is detected after the keyword
    public bool AcceptAdditionalText { get; set; }

    //Placeholder value, can be used to describe what a command does (modded/vanilla)
    public string Description { get; set; }

    //Placeholder value, can be used in the future to categorize commands (modded/vanilla)
    public string Category { get; set; }

    //Both Category/Description could be used in an interactive style menu listing all possible keywords that can be run from the terminal
}
