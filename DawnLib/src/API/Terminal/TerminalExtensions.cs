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
        if(node == null) return false;
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
}
