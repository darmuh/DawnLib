using System;
using System.Collections.Generic;
using System.Text;
using Dawn.Internal;

namespace Dawn.src;

internal class DawnTesting
{
    internal static void TestCommands()
    {
        //doesn't detect command due to space between words

        TerminalCommandRegistrationBuilder version = new("dawnlib_version", () => $"DawnLib version {MyPluginInfo.PLUGIN_VERSION}\n\n");
        version.SetEnabled(new FuncProvider<bool>(ShouldAddVersion));
        version.SetKeywords(new SimpleProvider<List<string>>(["dawn version", "version"]));
        version.SetCategory("Test");
        version.SetClearText(false);
        version.BuildOnTerminalAwake();

        //working
        TerminalCommandRegistrationBuilder lights = new("dawnlib_lights", BasicLightsCommand);
        lights.SetEnabled(new SimpleProvider<bool>(true));
        lights.SetKeywords(new FuncProvider<List<string>>(LightsKeywords)); //dynamic keywords via FuncProvider
        lights.SetCategory("Test");
        lights.BuildOnTerminalAwake();

        //no input = null error, with input = command not recognized
        TerminalCommandRegistrationBuilder testinput = new("test_input", InputCommandExample);
        testinput.SetEnabled(new SimpleProvider<bool>(true));
        testinput.SetKeywords(new SimpleProvider<List<string>>(["input"]));
        testinput.SetCategory("Test");
        testinput.SetAcceptInput(true); //will accept the command with input after the keyword provided
        testinput.BuildOnTerminalAwake();

        //query results in result node for some reason
        TerminalCommandRegistrationBuilder testQuery = new("test_query", () => "You have selected, YES!\n\n");
        testQuery.SetEnabled(new SimpleProvider<bool>(true));
        testQuery.SetKeywords(new SimpleProvider<List<string>>(["query", "check"]));
        testQuery.SetCategory("Test");
        testQuery.SetupQuery(() => "This is a test query, respond [YES] or [NO]\n\n");
        testQuery.SetupCancel(() => "You have selected, NO!\n\n");
        testQuery.SetContinueWord("yes");
        testQuery.SetCancelWord("no");
        testQuery.SetClearText(true);
        testQuery.SetDescription("Test query command with added compatible nouns");
        testQuery.BuildOnTerminalAwake();

    }

    private static string BasicLightsCommand()
    {
        StartOfRound.Instance.shipRoomLights.ToggleShipLights();
        if (StartOfRound.Instance.shipRoomLights.areLightsOn)
            return $"Ship Lights are [ON]\n\n";
        else
            return $"Ship Lights are [OFF]\n\n";
    }

    private static string InputCommandExample()
    {
        string cleanedText = TerminalRefs.Instance.screenText.text[^TerminalRefs.Instance.textAdded..];
        if (string.IsNullOrEmpty(TerminalRefs.Instance.GetLastCommand()))
            cleanedText = string.Empty;
        else
            cleanedText = cleanedText.Replace(TerminalRefs.Instance.GetLastCommand(), "").Trim();
        return $"This is a test command displaying user input after the command.\n\nUser input is [ {cleanedText} ]\n\n";
    }

    private static bool ShouldAddVersion()
    {
        return true;
    }

    private static List<string> LightsKeywords()
    {
        return ["lights", "dark"];
    }
}
