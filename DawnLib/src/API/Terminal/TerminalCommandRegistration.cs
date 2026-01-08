using System;
using System.Collections.Generic;
using Dawn.Internal;
using UnityEngine.Events;

namespace Dawn;

//for use with creating terminal commands from plugin awake
internal class TerminalCommandRegistration
{
    public string Name = string.Empty;
    public IProvider<bool> IsEnabled = null!;
    public bool DoesClearText = true;
    public string Category = string.Empty;
    public string Description = string.Empty;
    public IProvider<List<string>> KeywordList = null!;

    public Func<string> ResultFunction = null!;

    //Query-Style
    public Func<string>? QueryFunction;
    public Func<string>? CancelFunction;
    public string? ContinueWord;
    public string? CancelWord;

    //for commands that accept input after the keyword (ie. "fov 90" where the command is <fov> and <90> is the additional input)
    //will apply to keyword via interface
    public bool AcceptAdditionalText = false;

    internal TerminalCommandRegistration(string commandName)
    {
        Name = commandName;
    }
}

public class TerminalCommandRegistrationBuilder(string CommandName, Func<string> mainFunction)
{
    private TerminalCommandRegistration register = new(CommandName)
    {
        ResultFunction = mainFunction
    };

    public TerminalCommandRegistrationBuilder SetKeywords(IProvider<List<string>> keywords)
    {
        register.KeywordList = keywords;
        return this;
    }

    public TerminalCommandRegistrationBuilder SetupQuery(Func<string> queryFunc)
    {
        register.QueryFunction = queryFunc;
        return this;
    }

    public TerminalCommandRegistrationBuilder SetupCancel(Func<string> cancelFunc)
    {
        register.CancelFunction = cancelFunc;
        return this;
    }

    public TerminalCommandRegistrationBuilder SetCancelWord(string value)
    {
        register.CancelWord = value;
        return this;
    }

    public TerminalCommandRegistrationBuilder SetContinueWord(string value)
    {
        register.ContinueWord = value;
        return this;
    }

    public TerminalCommandRegistrationBuilder BuildOnTerminalAwake()
    {
        TerminalPatches.OnTerminalAwake += Build;
        return this;
    }


    //NOTE: The event in this param must invoke AFTER Terminal Awake in order to work
    public TerminalCommandRegistrationBuilder SetCustomBuildEvent(UnityEvent buildEvent)
    {
        buildEvent.AddListener(Build);
        return this;
    }

    private void HandleCustomBuildEvent(object sender, EventArgs e)
    {
        Build();
    }

    public TerminalCommandRegistrationBuilder SetDescription(string description)
    {
        register.Description = description;
        return this;
    }

    public TerminalCommandRegistrationBuilder SetCategory(string category)
    {
        register.Category = category;
        return this;
    }

    public TerminalCommandRegistrationBuilder SetClearText(bool value)
    {
        register.DoesClearText = value;
        return this;
    }

    public TerminalCommandRegistrationBuilder SetEnabled(IProvider<bool> value)
    {
        register.IsEnabled = value;
        return this;
    }

    public TerminalCommandRegistrationBuilder SetAcceptInput(bool value)
    {
        register.AcceptAdditionalText = value;
        return this;
    }

    private void Build()
    {
        DawnPlugin.Logger.LogDebug($"Attempting to build command [{register.Name}]");

        if (!ShouldBuild())
        {
            DawnPlugin.Logger.LogWarning($"Unable to build command [{register.Name}] due to missing required components!");
            return;
        }

        TerminalNodeBuilder resultbuilder = new($"{register.Name}_node");
        resultbuilder.SetDisplayText($"{register.Name} command");
        resultbuilder.SetClearPreviousText(register.DoesClearText);
        TerminalNode resultNode = resultbuilder.Build();
        List<TerminalKeyword> keywords = [];
        List<string> words = register.KeywordList.Provide();

        foreach (var word in words)
        {
            DawnPlugin.Logger.LogDebug($"Creating keyword [ {word} ] for command [ {register.Name} ]");
            TerminalKeywordBuilder addWord = new($"{register.Name}_{word}", word, ITerminalKeyword.KeywordType.Command);
            addWord.SetAcceptInput(register.AcceptAdditionalText);
            keywords.Add(addWord.Build());
        }

        TerminalCommandBuilder commandbuilder = new(register.Name);
        commandbuilder.SetResultNode(resultNode);
        commandbuilder.AddResultAction(register.ResultFunction);
        commandbuilder.AddKeyword(keywords);

        if (IsQueryCommand())
        {
            TerminalNodeBuilder queryBuilder = new($"{register.Name}_Query");
            queryBuilder.SetDisplayText($"{register.Name} Query");
            queryBuilder.SetClearPreviousText(true); //add this as a property or just keep it default true?
            var queryNode = queryBuilder.Build();
            commandbuilder.SetQueryNode(queryNode);

            TerminalNodeBuilder cancelBuilder = new($"{register.Name}_Cancel");
            cancelBuilder.SetDisplayText($"{register.Name} Cancel");
            cancelBuilder.SetClearPreviousText(true); //add this as a property or just keep it default true?
            var cancelNode = cancelBuilder.Build();
            commandbuilder.SetCancelNode(cancelBuilder.Build());

            commandbuilder.SetCancelWord(register.CancelWord!);
            commandbuilder.SetContinueWord(register.ContinueWord!);
            commandbuilder.AddCancelAction(register.CancelFunction!);
            commandbuilder.AddQueryAction(register.QueryFunction!);
        }

        commandbuilder.FinishBuild();

    }

    private bool ShouldBuild()
    {
        if (!register.IsEnabled.Provide())
            return false;

        if (register.KeywordList.Provide().Count == 0) return false;

        return true;
    }

    private bool IsQueryCommand()
    {
        return register.QueryFunction != null && register.CancelFunction != null && register.ContinueWord != null && register.CancelWord != null;
    }
}
