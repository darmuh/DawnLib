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
        return ((ITerminalNode)node).DawnNodeFunction;
    }

    internal static bool HasCommandFunction(this TerminalNode node)
    {
        if (node == null) return false;
        return node.GetCommandFunction() != null;
    }

    internal static void SetNodeFunction(this TerminalNode node, Func<string> NodeFunc)
    {
        ((ITerminalNode)node).DawnNodeFunction = NodeFunc;
    }

    public static bool GetKeywordAcceptInput(this TerminalKeyword word)
    {
        return ((ITerminalKeyword)word).DawnAcceptAdditionalText;
    }

    public static void SetKeywordAcceptInput(this TerminalKeyword word, bool value)
    {
        ((ITerminalKeyword)word).DawnAcceptAdditionalText = value;
    }

    public static string GetKeywordCategory(this TerminalKeyword word)
    {
        return ((ITerminalKeyword)word).DawnKeywordCategory;
    }

    public static void SetKeywordCategory(this TerminalKeyword word, string value)
    {
        ((ITerminalKeyword)word).DawnKeywordCategory = value;
    }

    public static string GetKeywordDescription(this TerminalKeyword word)
    {
        return ((ITerminalKeyword)word).DawnKeywordDescription;
    }

    public static void SetKeywordDescription(this TerminalKeyword word, string value)
    {
        ((ITerminalKeyword)word).DawnKeywordDescription = value;
    }

    internal static void SetLastCommand(this Terminal terminal, string value)
    {
        ((ITerminal)terminal).DawnLastCommand = value;
    }

    public static string GetLastCommand(this Terminal terminal)
    {
        return ((ITerminal)terminal).DawnLastCommand;
    }

    internal static void SetLastKeyword(this Terminal terminal, TerminalKeyword value)
    {
        ((ITerminal)terminal).DawnLastKeyword = value;
    }

    public static TerminalKeyword GetLastKeyword(this Terminal terminal)
    {
        return ((ITerminal)terminal).DawnLastKeyword;
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

    public static bool DawnTryResolveKeyword(this Terminal terminal, string input, out TerminalKeyword word)
    {
        word = null!;
        int maxScore = 0;

        //empty input, return false
        if(string.IsNullOrWhiteSpace(input)) return false;

        //only get words that start with our input to start
        List<TerminalKeyword> keywordList = [.. terminal.terminalNodes.allKeywords.Where(x => x.word.StringStartsWithInvariant(input))];
        
        //follow vanilla logic of not getting more than one verb
        if (terminal.hasGottenVerb)
            keywordList.RemoveAll(x => x.isVerb);
        
        //no matches
        if(keywordList.Count == 0)
            return false;
        
        //only one match
        if (keywordList.Count == 1)
        {
            word = keywordList[0];
            return word != null;
        }

        //multiple matches expected
        Dictionary<TerminalKeyword, int> wordScores = [];
        foreach(TerminalKeyword keyword in keywordList)
        {
            var score = keyword.word.StringMatchScore(input);
            wordScores.TryAdd(keyword, score);
        }

        foreach(var match in wordScores)
        {
            if (match.Key == null) continue; //skip null terminalkeywords (just in case)
            if(word == null || maxScore == 0)
            {
                word = match.Key;
                maxScore = match.Value;
                continue;
            }

            //skip since this partial match has a lower score
            if (maxScore > match.Value)
                continue;

            //this match has the same amount of matching characters
            //resolve conflict by checking keyword priority (lower number = higher priority)
            if (maxScore == match.Value)
            {
                //checks if the current match has a keyword priority value lower than the match assigned to the word variable (working match)
                //a lower keyword priority value indicates a higher priority keyword
                DawnPlugin.Logger.LogDebug($"Attempting to resolve conflict between matching results [{word.word}] & [{match.Key.word}] by comparing keyword priorities!");
                int target = (int)word.GetKeywordPriority();
                int current = (int)match.Key.GetKeywordPriority();

                if (current < target)
                {
                    word = match.Key; //only need to update the word
                    continue;
                }
            }

            DawnPlugin.Logger.LogDebug($"Skipping partial match [{match.Key.word}] with match score {match.Value} due to better match existing with a higher priority");
        }

        terminal.SetLastKeyword(word);
        DawnPlugin.Logger.LogMessage($"DawnTryResolveKeyword has found match with highest priority of {word.word} ({word.GetKeywordPriority()})");
        return word != null;
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
            terminalKeyword.SetKeywordPriority(ITerminalKeyword.DawnKeywordType.Core);
            return;
        }

        if (terminalKeyword.accessTerminalObjects)
        {
            terminalKeyword.SetKeywordPriority(ITerminalKeyword.DawnKeywordType.Code);
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

        terminalKeyword.SetKeywordPriority(ITerminalKeyword.DawnKeywordType.Other);
    }

    //vanilla keywords that should probably not be replaced unless the API user is intending to overwrite a core function of the game
    private static readonly List<string> VanillaWords = ["company", "moons", "store", "help", "other", "bestiary", "storage", "scan", "upgrades", "decor", "sigurd"];
    public static ITerminalKeyword.DawnKeywordType TryGetTerminalNodeType(this TerminalNode node)
    {
        if (node == null)
        {
            DawnPlugin.Logger.LogDebug("Null TerminalNode provided to TryGetTerminalNodeType, returning lowest priority");
            return ITerminalKeyword.DawnKeywordType.Other;
        }

        //just assuming any node with a terminal event string is a core gameplay element
        //vanilla examples are eject & switch
        if (!string.IsNullOrEmpty(node.terminalEvent))
            return ITerminalKeyword.DawnKeywordType.Core;

        //moon keywords
        if (node.buyRerouteToMoon > -1 || node.displayPlanetInfo > -1)
            return ITerminalKeyword.DawnKeywordType.Moons;

        //vehicle keywords
        if (node.buyVehicleIndex > -1)
            return ITerminalKeyword.DawnKeywordType.Vehicles;

        //shop keywords
        if (node.shipUnlockableID > -1 || node.buyItemIndex > -1)
            return ITerminalKeyword.DawnKeywordType.Store;

        //bestiary keywords
        if (node.creatureFileID > -1)
            return ITerminalKeyword.DawnKeywordType.Bestiary;

        //log keywords
        if (node.storyLogFileID > -1)
            return ITerminalKeyword.DawnKeywordType.SigurdLog;

        //command keywords
        if (node.HasCommandFunction())
            return ITerminalKeyword.DawnKeywordType.DawnCommand;

        //no matching types
        return ITerminalKeyword.DawnKeywordType.Other;
    }

    public static ITerminalKeyword.DawnKeywordType GetKeywordPriority(this TerminalKeyword word)
    {
        return ((ITerminalKeyword)word).DawnKeywordPriority;
    }

    public static void SetKeywordPriority(this TerminalKeyword word, ITerminalKeyword.DawnKeywordType value)
    {
        ((ITerminalKeyword)word).DawnKeywordPriority = value;
    }

}
