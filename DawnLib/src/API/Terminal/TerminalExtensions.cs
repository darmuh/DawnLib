using System;
using System.Collections.Generic;
using System.Linq;
using Dawn.Internal;
using Dawn.Utils;

namespace Dawn;

public static class TerminalExtensions
{
    public static Func<string> GetCommandFunction(this TerminalNode node)
    {
        return ((ITerminalNode)node).NodeFunction;
    }

    internal static bool HasCommandFunction(this TerminalNode node)
    {
        if (node == null) return false;
        return node.GetCommandFunction() != null;
    }

    internal static void SetNodeFunction(this TerminalNode node, Func<string> NodeFunc)
    {
        ((ITerminalNode)node).NodeFunction = NodeFunc;
    }

    public static bool GetKeywordAcceptInput(this TerminalKeyword word)
    {
        return ((ITerminalKeyword)word).AcceptAdditionalText;
    }

    public static void SetKeywordAcceptInput(this TerminalKeyword word, bool value)
    {
        ((ITerminalKeyword)word).AcceptAdditionalText = value;
    }

    public static string GetKeywordCategory(this TerminalKeyword word)
    {
        return ((ITerminalKeyword)word).Category;
    }

    public static void SetKeywordCategory(this TerminalKeyword word, string value)
    {
        ((ITerminalKeyword)word).Category = value;
    }

    public static string GetKeywordDescription(this TerminalKeyword word)
    {
        return ((ITerminalKeyword)word).Description;
    }

    public static void SetKeywordDescription(this TerminalKeyword word, string value)
    {
        ((ITerminalKeyword)word).Description = value;
    }

    internal static void SetLastCommand(this Terminal terminal, string value)
    {
        ((ITerminal)terminal).LastCommand = value;
    }

    public static string GetLastCommand(this Terminal terminal)
    {
        return ((ITerminal)terminal).LastCommand;
    }

    public static bool TryGetKeywordInfoText(this TerminalKeyword word, out string text)
    {
        text = string.Empty;
        var match = TerminalRefs.InfoKeyword.compatibleNouns.FirstOrDefault(x => x.noun.word == word.word);
        if (match == null)
            return false;

        text = match.result.displayText;
        return true;
    }

    public static bool TryGetKeyword(this Terminal terminal, string keyWord, out TerminalKeyword terminalKeyword)
    {
        List<TerminalKeyword> keyWordList = [.. terminal.terminalNodes.allKeywords];

        foreach (TerminalKeyword keyword in keyWordList)
        {
            if (keyWord.CompareStringsInvariant(keyword.word))
            {
                //Loggers.LogDebug($"Keyword: [{keyWord}] found!");
                terminalKeyword = keyword;
                return true;
            }
        }

        terminalKeyword = null!;
        return false;
    }

    internal static void TryAssignType(this TerminalKeyword terminalKeyword)
    {
        //don't try to reset priorities that have already been assigned
        if (terminalKeyword.GetKeywordPriority() != 0)
            return;

        if (terminalKeyword.isVerb || VanillaWords.Contains(terminalKeyword.word.ToLowerInvariant()))
        {
            terminalKeyword.SetKeywordPriority(ITerminalKeyword.KeywordType.VanillaCore);
            return;
        }

        if (terminalKeyword.accessTerminalObjects)
        {
            terminalKeyword.SetKeywordPriority(ITerminalKeyword.KeywordType.Code);
            return;
        }

        if (terminalKeyword.specialKeywordResult != null)
        {
            terminalKeyword.SetKeywordPriority(terminalKeyword.specialKeywordResult.TryGetTerminalNodeType());
            return;
        }

        if (terminalKeyword.defaultVerb != null)
        {
            var match = terminalKeyword.defaultVerb.compatibleNouns.FirstOrDefault(x => x.noun.word.CompareStringsInvariant(terminalKeyword.word));
            if (match != null)
            {
                var priority = match.result.TryGetTerminalNodeType();
                terminalKeyword.SetKeywordPriority(priority);
                DawnPlugin.Logger.LogDebug($"{terminalKeyword.word} priority set to {priority}");
                return;
            }
            else
                DawnPlugin.Logger.LogDebug($"Unable to determine keyword type for word: [ {terminalKeyword.word} ]\nKeywordPriority is set to other!");
        }

        terminalKeyword.SetKeywordPriority(ITerminalKeyword.KeywordType.Other);
    }

    //vanilla keywords that should probably not be replaced unless the API user is intending to overwrite a core function of the game
    private static readonly List<string> VanillaWords = ["company", "moons", "store", "help", "other", "bestiary", "storage", "scan", "upgrades", "decor", "sigurd"];
    public static ITerminalKeyword.KeywordType TryGetTerminalNodeType(this TerminalNode node)
    {
        if (node == null)
        {
            DawnPlugin.Logger.LogDebug("Null TerminalNode provided to TryGetTerminalNodeType, returning lowest priority");
            return ITerminalKeyword.KeywordType.Other;
        }

        //just assuming any node with a terminal event string is a core gameplay element
        //vanilla examples are eject & switch
        if (!string.IsNullOrEmpty(node.terminalEvent))
            return ITerminalKeyword.KeywordType.VanillaCore;

        //moon keywords
        if (node.buyRerouteToMoon > -1 || node.displayPlanetInfo > -1)
            return ITerminalKeyword.KeywordType.Moon;

        //vehicle keywords
        if (node.buyVehicleIndex > -1)
            return ITerminalKeyword.KeywordType.VehicleItem;

        //shop keywords
        if (node.shipUnlockableID > -1 || node.buyItemIndex > -1)
            return ITerminalKeyword.KeywordType.ShopItem;

        //bestiary keywords
        if (node.creatureFileID > -1)
            return ITerminalKeyword.KeywordType.BestiaryItem;

        //log keywords
        if (node.storyLogFileID > -1)
            return ITerminalKeyword.KeywordType.StoryLogItem;

        //command keywords
        if (node.HasCommandFunction())
            return ITerminalKeyword.KeywordType.Command;

        //no matching types
        return ITerminalKeyword.KeywordType.Other;
    }

    public static ITerminalKeyword.KeywordType GetKeywordPriority(this TerminalKeyword word)
    {
        return ((ITerminalKeyword)word).KeywordPriority;
    }

    public static void SetKeywordPriority(this TerminalKeyword word, ITerminalKeyword.KeywordType value)
    {
        ((ITerminalKeyword)word).KeywordPriority = value;
    }

}
