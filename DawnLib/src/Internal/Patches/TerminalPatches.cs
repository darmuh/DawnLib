using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using Dawn.Utils;
using Mono.Cecil.Cil;
using MonoMod.Cil;
using UnityEngine;

namespace Dawn.Internal;
static class TerminalPatches
{
    internal static event Action OnTerminalAwake = delegate { };
    internal static event Action OnTerminalDisable = delegate { };
    internal static void Init()
    {
        On.Terminal.Awake += TerminalAwakeHook;
        On.Terminal.Start += TerminalStartHook;
        On.Terminal.OnDisable += TerminalDisableHook;
        On.Terminal.ParsePlayerSentence += HandleDawnCommand;
        On.Terminal.LoadNewNodeIfAffordable += HandlePredicate;
        On.Terminal.TextPostProcess += UpdateItemPrices;
        IL.Terminal.TextPostProcess += HideResults;
        IL.Terminal.TextPostProcess += UseFailedNameResults;
    }

    private static void TerminalDisableHook(On.Terminal.orig_OnDisable orig, Terminal self)
    {
        //All commands use this event to destroy themselves between lobby loads
        OnTerminalDisable.Invoke();

        //still need to run the method
        orig(self);
    }

    private static void TerminalStartHook(On.Terminal.orig_Start orig, Terminal self)
    {
        orig(self);

        //assign priorities to any remaining keywords that have not received a value yet
        //also assign descriptions/category if unassigned
        //doing this in start to give time after Terminal.Awake where commands are created
        foreach (var keyword in self.terminalNodes.allKeywords)
        {
            keyword.TryAssignType();
            if (String.IsNullOrEmpty(keyword.GetKeywordCategory()))
                keyword.SetKeywordCategory(keyword.GetKeywordPriority().ToString());

            if (String.IsNullOrEmpty(keyword.GetKeywordDescription()))
            {
                if (keyword.TryGetKeywordInfoText(out string result))
                    keyword.SetKeywordDescription(result.Trim());
                else
                    keyword.SetKeywordDescription($"No information on the terminal keyword [ {keyword.word} ]");
            }
        }
    }

    private static void TerminalAwakeHook(On.Terminal.orig_Awake orig, Terminal self)
    {
        orig(self);
        //below will have many terminal commands begin building on the below invoke
        //only commands with a custom defined build event will not use this event
        OnTerminalAwake.Invoke();
    }

    private static TerminalNode GetNodeFromArray(Terminal self, string[] array)
    {
        TerminalKeyword verb = null!;
        TerminalKeyword noun = null!;

        foreach (string word in array)
        {
            //vanilla terminal only expects one verb and one noun!
            if (noun != null && verb != null)
                break;

            if (self.DawnTryResolveKeyword(word, out TerminalKeyword result))
            {
                if (result.accessTerminalObjects)
                {
                    self.CallFunctionInAccessibleTerminalObject(result.word);
                    self.PlayBroadcastCodeEffect();
                    return null!; //this is what zeekers does for these
                }

                if (result.isVerb)
                {
                    if (verb != null)
                        continue;

                    verb = result;
                }
                else
                {
                    if (noun != null)
                        continue;

                    noun = result;

                    //input based result for DawnLib Commands
                    if (result.GetKeywordAcceptInput() && result.specialKeywordResult != null)
                    {
                        self.SetLastCommand(word);
                        self.SetLastKeyword(result);
                        return result.specialKeywordResult;
                    }
                }
            }
        }

        //set this for any other potential purposes to the full input
        //could also be set to just the noun portion of the input or the full noun keyword
        //not sure what the use-case would be so for now just including the full array
        self.SetLastCommand(string.Concat(array));

        //failed to parse vanilla equivalent
        if (noun == null)
            return self.terminalNodes.specialNodes[10];

        //automatically set default verb like vanilla
        if (verb == null && noun.defaultVerb != null)
            verb = noun.defaultVerb;
        else
            return self.terminalNodes.specialNodes[11];

        //vanilla equivalent of trying to find noun in the given verb's compatible nouns
        var nounResult = verb.compatibleNouns.FirstOrDefault(x => x.noun == noun);
        if (nounResult == null)
        {
            //failed to find noun in verb's compatible nouns, returning failed parse node like vanilla
            return self.terminalNodes.specialNodes[12];
        }    
        else
        {
            //again unsure of use-case for this particular property but I assume people would only care about VALID nouns
            self.SetLastKeyword(nounResult.noun);
            return nounResult.result;
        }
            
    }

    //perhaps this may be better as an IL patch
    private static TerminalNode HandleDawnCommand(On.Terminal.orig_ParsePlayerSentence orig, Terminal self)
    {
        //reset LastCommand value
        //Cannot be set based on terminalNode as nodes can have multiple keywords
        self.SetLastCommand(string.Empty);
        //reset this value as well, api users should expect a potential null value
        self.SetLastKeyword(null!);

        //getting the values we need to override ParsePlayerSentence
        TerminalNode terminalNode = null!;
        string s = self.screenText.text[^self.textAdded..];
        s = self.RemovePunctuation(s);
        string[] array = s.Split(' ', StringSplitOptions.RemoveEmptyEntries);

        //let vanilla handle these words specifically
        List<string> VanillaSwitch = ["switch", "flash", "ping", "transmit"];

        if (array.Length > 1 && VanillaSwitch.Contains(array[0]))
        {
            //just run vanilla method for these
            terminalNode = orig(self);
        }   
        else
        {
            //-- reused vanilla code from ParsePlayerSentence
            string value = Regex.Match(s, "\\d+").Value;
            if (!string.IsNullOrWhiteSpace(value))
            {
                self.playerDefinedAmount = Mathf.Clamp(int.Parse(value), 0, 10);
            }
            else
            { 
                self.playerDefinedAmount = 1;
            }
            //-- used to determine number value provided by players

            //check full input first
            if (self.DawnTryResolveKeyword(s, out TerminalKeyword sentence))
            {
                self.SetLastCommand(sentence.word);

                if (sentence.specialKeywordResult != null)
                    terminalNode = sentence.specialKeywordResult;

                //door codes, turrets, etc. are not handled via terminal node
                if (sentence.accessTerminalObjects)
                {
                    self.CallFunctionInAccessibleTerminalObject(sentence.word);
                    self.PlayBroadcastCodeEffect();
                    return null!; //this is what zeekers does for these
                }
            }
            else
            {
                //parse array of words for a result node
                terminalNode = GetNodeFromArray(self, array);
            }
        }

        //updates the node's displaytext based on it's NodeFunction Func<string> that was injected (if not null)
        if (terminalNode.HasCommandFunction())
            terminalNode.displayText = terminalNode.GetCommandFunction().Invoke();

        return terminalNode;
    }

    // this is currently a separate function because this is very specific to vanilla
    private static void HideResults(ILContext il)
    {
        ILCursor c = new ILCursor(il);
        ILLabel loopStart = null!; // make compiler happy with null!
        Debuggers.Patching?.Log($"transpiling {il.Method.Name} with {nameof(HideResults)}. instructions: {c.Instrs.Count}");

        c.GotoNext(
            i => i.MatchLdloc(7),
            i => i.MatchLdarg(0),
            i => i.MatchLdfld<Terminal>(nameof(Terminal.buyableItemsList)),
            i => i.MatchLdlen(),
            i => i.MatchConvI4(),
            i => i.MatchBlt(out loopStart)
        );
        int targetIndex = c.Index + 2;
        Debuggers.Patching?.Log($"target index = {targetIndex}");

        Debuggers.Patching?.Log($"loopStart = {loopStart}, target = {loopStart.Target}, offset = {loopStart.Target.Offset}");
        c.GotoLabel(loopStart);
        c.Emit(OpCodes.Ldarg_0);
        c.EmitLdfld<Terminal>(nameof(Terminal.buyableItemsList));
        c.EmitLdloc(7);
        c.Emit(OpCodes.Ldelem_Ref);
        c.EmitDelegate((Item item) =>
        {
            Debuggers.Items?.Log($"Checking {item.itemName}");
            DawnItemInfo info = item.GetDawnInfo();
            DawnShopItemInfo? shopInfo = info.ShopInfo;
            if (shopInfo == null)
                return true;

            TerminalPurchaseResult result = shopInfo.DawnPurchaseInfo.PurchasePredicate.CanPurchase();

            if (result is TerminalPurchaseResult.HiddenPurchaseResult)
            {
                Debuggers.Items?.Log($"Hiding {info.Key}");
                return false;
            }
            return true;
        });
        c.Emit(OpCodes.Brfalse, c.Instrs[targetIndex]);
        Debuggers.Patching?.Log("did shopitem hidden patch!");

        c.Index = 0;
        c.GotoNext(
            i => i.MatchLdloc(14),
            i => i.MatchLdcI4(1),
            i => i.MatchAdd(),
            i => i.MatchStloc(14)
        );
        c.GotoNext(
            i => i.MatchBlt(out loopStart)
        );
        targetIndex = c.Index + 2;

        c.GotoLabel(loopStart);

        c.Emit(OpCodes.Ldarg_0);
        c.EmitLdfld<Terminal>(nameof(Terminal.ShipDecorSelection));
        c.EmitLdloc(14);
        c.EmitCallvirt<List<TerminalNode>>("get_Item");
        c.EmitDelegate((TerminalNode unlockableNode) =>
        {
            if (unlockableNode.shipUnlockableID < 0 || unlockableNode.shipUnlockableID > StartOfRound.Instance.unlockablesList.unlockables.Count)
            {
                DawnPlugin.Logger.LogWarning($"{unlockableNode.creatureName} ({unlockableNode.name}) has a ship unlockable id of {unlockableNode.shipUnlockableID} which doesn't make sense.");
                return true;
            }
            UnlockableItem unlockableItem = StartOfRound.Instance.unlockablesList.unlockables[unlockableNode.shipUnlockableID];
            DawnUnlockableItemInfo? info = unlockableItem.GetDawnInfo();
            if (info == null)
            {
                DawnPlugin.Logger.LogWarning($"{unlockableNode.creatureName} ({unlockableNode.name}) of {unlockableItem.unlockableName} has no dawn info.");
                return true;
            }
            TerminalPurchaseResult result = info.DawnPurchaseInfo.PurchasePredicate.CanPurchase();
            if (result is TerminalPurchaseResult.HiddenPurchaseResult)
            {
                Debuggers.Unlockables?.Log($"Hiding {info.Key}");
                return false;
            }
            return true;
        });
        c.Emit(OpCodes.Brfalse, c.Instrs[targetIndex]);
    }


    private static string UpdateItemPrices(On.Terminal.orig_TextPostProcess orig, Terminal self, string modifieddisplaytext, TerminalNode node)
    {
        ItemRegistrationHandler.UpdateAllShopItemPrices();
        MoonRegistrationHandler.UpdateAllPrices();
        return orig(self, modifieddisplaytext, node);
    }

    internal static void UseFailedNameResults(ILContext il)
    {
        ILCursor c = new ILCursor(il);
        DawnPlugin.Logger.LogDebug($"transpiling {il.Method.Name} with {nameof(UseFailedNameResults)}. instructions: {c.Instrs.Count}");
        if (c.TryGotoNext(
            i => i.MatchLdfld<Item>(nameof(Item.itemName))
        ))
        {
            c.Next.OpCode = OpCodes.Nop;

            c.EmitDelegate<Func<Item, string>>((item) =>
            {
                if (!item.HasDawnInfo())
                {
                    DawnPlugin.Logger.LogWarning($"Item: {item.itemName} hasn't been found by DawnLib prior to the terminal being run, please report this!");
                    return item.itemName;
                }
                DawnItemInfo info = item.GetDawnInfo();
                DawnShopItemInfo? shopInfo = info.ShopInfo;
                if (shopInfo == null)
                    return item.itemName;

                TerminalPurchaseResult result = shopInfo.DawnPurchaseInfo.PurchasePredicate.CanPurchase();
                if (result is TerminalPurchaseResult.FailedPurchaseResult failedResult)
                {
                    if (failedResult.OverrideName != null)
                    {
                        Debuggers.Items?.Log($"Overriding name of {info.Key} with {failedResult.OverrideName}");
                    }
                    return failedResult.OverrideName ?? item.itemName;
                }

                return item.itemName;
            });
        }

        c.Index = 0;
        if (c.TryGotoNext(
            i => i.MatchLdfld<UnlockableItem>(nameof(UnlockableItem.unlockableName))
        ))
        {
            c.Next.OpCode = OpCodes.Nop;

            c.EmitDelegate((UnlockableItem unlockable) =>
            {
                if (!unlockable.HasDawnInfo())
                {
                    DawnPlugin.Logger.LogWarning($"Unlockable: {unlockable.unlockableName} hasn't been found by DawnLib prior to the terminal being run, please report this!");
                    return unlockable.unlockableName;
                }
                DawnUnlockableItemInfo info = unlockable.GetDawnInfo();
                TerminalPurchaseResult result = info.DawnPurchaseInfo.PurchasePredicate.CanPurchase();
                if (result is TerminalPurchaseResult.FailedPurchaseResult failedResult)
                {
                    if (failedResult.OverrideName != null)
                    {
                        Debuggers.Unlockables?.Log($"Overriding name of {info.Key} with {failedResult.OverrideName}");
                    }
                    return failedResult.OverrideName;
                }

                return unlockable.unlockableName;
            });
        }
    }
    private static void HandlePredicate(On.Terminal.orig_LoadNewNodeIfAffordable orig, Terminal self, TerminalNode node)
    {
        Debuggers.Patching?.Log($"HandlePredicate: {node}");

        ItemRegistrationHandler.UpdateAllShopItemPrices();
        UnlockableRegistrationHandler.UpdateAllUnlockablePrices();

        ITerminalPurchase? purchase = null;
        if (node.buyItemIndex != -1)
        {
            Debuggers.Patching?.Log($"buyItemIndex = {node.buyItemIndex}");
            Item buyingItem = self.buyableItemsList[node.buyItemIndex];
            if (!buyingItem.HasDawnInfo())
            {
                DawnPlugin.Logger.LogWarning($"Item: {buyingItem.itemName} hasn't been found by DawnLib prior to the terminal being run, please report this!");
                orig(self, node);
                return;
            }

            DawnItemInfo info = buyingItem.GetDawnInfo();
            DawnShopItemInfo? shopItemInfo = info.ShopInfo;

            if (shopItemInfo != null)
            {
                purchase = shopItemInfo.DawnPurchaseInfo;
            }
        }

        if (node.shipUnlockableID != -1)
        {
            Debuggers.Patching?.Log($"shipUnlockableID = {node.shipUnlockableID}");

            UnlockableItem unlockableItem = StartOfRound.Instance.unlockablesList.unlockables[node.shipUnlockableID];
            if (!unlockableItem.HasDawnInfo())
            {
                DawnPlugin.Logger.LogWarning($"Unlockable: {unlockableItem.unlockableName} hasn't been found by DawnLib prior to the terminal being run, please report this!");
                orig(self, node);
                return;
            }
            DawnUnlockableItemInfo? info = unlockableItem.GetDawnInfo();
            purchase = info.DawnPurchaseInfo;
        }

        if (node.buyRerouteToMoon >= 0)
        {
            Debuggers.Patching?.Log($"buyRerouteToMoon = {node.buyRerouteToMoon}");
            purchase = StartOfRound.Instance.levels[node.buyRerouteToMoon].GetDawnInfo().DawnPurchaseInfo;
        }

        // preform predicate
        if (purchase != null)
        {
            Debuggers.Patching?.Log($"has predicate");

            TerminalPurchaseResult result = purchase.PurchasePredicate.CanPurchase();
            if (result is TerminalPurchaseResult.FailedPurchaseResult failedResult)
            {
                Debuggers.Patching?.Log($"predicate fail");

                orig(self, failedResult.ReasonNode);
                return;
            }

            if (result is TerminalPurchaseResult.HiddenPurchaseResult hiddenResult && hiddenResult.IsFailure)
            {
                Debuggers.Patching?.Log($"predicate hidden");

                self.LoadNewNode(hiddenResult.ReasonNode);
                return; // skip orig
            }
        }

        orig(self, node);
    }
}