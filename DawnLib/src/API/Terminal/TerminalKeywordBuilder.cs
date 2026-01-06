using System;
using System.Collections.Generic;
using UnityEngine;
using Dawn.Internal;

namespace Dawn;
public class TerminalKeywordBuilder
{
    private TerminalKeyword _keyword;

    internal TerminalKeywordBuilder(string name, string word)
    {
        if (TerminalRefs.Instance.TryGetKeyword(word, out _keyword))
        {
            throw new ArgumentException($"'{word}' already has an existing TerminalKeyword.");
        }
            
        _keyword = ScriptableObject.CreateInstance<TerminalKeyword>();
        _keyword.name = name;
        SetWord(word);
    }

    public TerminalKeywordBuilder SetWord(string word)
    {
        _keyword.word = word;
        return this;
    }

    public TerminalKeywordBuilder SetIsVerb(bool isVerb)
    {
        _keyword.isVerb = isVerb;
        return this;
    }

    public TerminalKeywordBuilder AddCompatibleNoun(CompatibleNoun compatibleNoun)
    {
        List<CompatibleNoun> compatibleNouns = [.. _keyword.compatibleNouns];
        compatibleNouns.Add(compatibleNoun);
        _keyword.compatibleNouns = [.. compatibleNouns];
        return this;
    }

    public TerminalKeywordBuilder SetSpecialKeywordResult(TerminalNode specialKeywordResult)
    {
        _keyword.specialKeywordResult = specialKeywordResult;
        return this;
    }

    public TerminalKeywordBuilder AccessTerminalObjects(bool accessTerminalObjects)
    {
        _keyword.accessTerminalObjects = accessTerminalObjects;
        return this;
    }

    public TerminalKeywordBuilder SetDefaultVerb(TerminalKeyword defaultVerb)
    {
        _keyword.defaultVerb = defaultVerb;
        return this;
    }

    public TerminalKeywordBuilder SetNodeFunction(Func<string> _function)
    {
        _keyword.specialKeywordResult.SetNodeFunction(_function);
        return this;
    }

    //only need to use this to set the value to true, default is false
    public TerminalKeywordBuilder SetAcceptInput(bool value)
    {
        _keyword.SetKeywordAcceptInput(value);
        return this;
    }

    public TerminalKeyword Build()
    {
        return _keyword;
    }
}