using System.Collections.Generic;
using System.Linq;
using System.Text;
using Dawn.Utils;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Dawn;

[RequireComponent(typeof(Terminal))]
public abstract class InteractiveTerminalMenu : MonoBehaviour
{
    public string MenuName = "InteractiveMenuBase";
    public List<TerminalMenuKey> KeyActions = [];
    private Terminal terminal;
    internal DawnEvent CheckInput = new();
    internal TerminalNode MenuNode;

    private int _activeIndex = 0;
    private int _endIndex = 0; // might not need this
    private int _currentPage = 1;
    private int _pageSize = 8;
    private List<TerminalMenuItem> DisplayMenuItemsOfType = [];

    public TerminalMenuItem RootMenuItem;
    private TerminalMenuItem? ActiveMenuItem;
    public string ActiveSelectionPrefix = "<mark=#ffff001A>";
    public string ActiveSelectionSuffix = "</mark>";

    // colors
    private Color Transparent = new(0f, 0f, 0f, 0f);

    /// <summary>
    /// Get the assigned ActiveMenuItem (nullable)
    /// </summary>
    public TerminalMenuItem? GetActiveMenuItem => ActiveMenuItem;

    /// <summary>
    /// (On monobehaviour awake) Assign your KeyEvents, create your terminal command, hook up the values, etc.
    /// This base assigns basic default controls, you can change this with an override.
    /// It also utilizes the <see cref="SetupCommand"/> base method to create an example command and hook up the terminalNode to this menu.
    /// </summary>
    public virtual void Awake()
    {
        // get terminal instance from game object this menu is attached to
        terminal = GetComponent<Terminal>();

        // base key actions
        KeyActions.Add(new(this, Key.UpArrow, () => SetCurrentItem(GetActiveIndex - 1)));
        KeyActions.Add(new(this, Key.DownArrow, () => SetCurrentItem(GetActiveIndex + 1)));
        KeyActions.Add(new(this, Key.LeftArrow, () => SetPage(GetCurrentPage - 1)));
        KeyActions.Add(new(this, Key.RightArrow, () => SetPage(GetCurrentPage + 1)));
        KeyActions.Add(new(this, Key.Enter, SelectActiveIndex));
        KeyActions.Add(new(this, Key.Backspace, () => ActiveMenuItem?.OnExitThis()));
    }

    /// <summary>
    /// This is a simple method that creates the keyword/node relationship needed to run this interactive terminal menu
    /// </summary>
    /// <param name="keywords">Keywords that can be used to start this menu</param>
    /// <remarks>
    /// This method is not run automatically and is just an example of how to setup the keyword/node relationship simply
    /// </remarks>
    public void SetupCommand(List<string> keywords)
    {
        TerminalNodeBuilder nodeBuilder = new(MenuName + "_node");
        nodeBuilder.SetDisplayText("interactive menu base (this should not show)");
        nodeBuilder.SetDynamicDisplayText(() => 
        {
            if (!enabled)
                enabled = true;

            return GetPageText();
        });
        nodeBuilder.SetClearPreviousText(true);
        MenuNode = nodeBuilder.Build();

        List<TerminalKeyword> keywordsCreated = [];
        foreach(string keyword in keywords.Distinct())
        {
            TerminalKeywordBuilder builder = new(MenuName + keyword, keyword);
            builder.SetSpecialKeywordResult(MenuNode);
            keywordsCreated.Add(builder.Build());
        }

        InjectKeywords(keywordsCreated);
    }

    /// <summary>
    /// Helper method for injecting keywords you've created for this menu
    /// </summary>
    /// <param name="terminalKeywords"></param>
    public void InjectKeywords(List<TerminalKeyword> terminalKeywords)
    {
        TerminalKeyword[] allKeywordsModified =
        [
            .. terminal.terminalNodes.allKeywords,
            .. terminalKeywords,
        ];

        terminal.terminalNodes.allKeywords = allKeywordsModified;
    }

    private void Update()
    {
        if (Keyboard.current.anyKey.wasPressedThisFrame)
        {
            CheckInput?.Invoke();
        }

        // terminal exited without disabling behaviour
        if (!terminal.placeableObject.inUse)
            enabled = false;
    }

    private void OnDisable()
    {
        if (GameNetworkManager.Instance.localPlayerController.inTerminalMenu)
            ExitMenu(true);
        else
            ExitMenu(false);
    }

    private void OnEnable()
    {
        if (RootMenuItem == null)
        {
            DawnPlugin.Logger.LogWarning($"Unable to open interactive menu [ {MenuName} ], RootMenuItem is null!");
            enabled = false;
            return;
        }

        ActiveMenuItem = RootMenuItem;
        //Keyboard.current.onTextInput += OnTextInput;
        EnterMenu();
    }

    /// <summary>
    /// Base method for entering the InteractiveTerminalMenu
    /// </summary>
    public virtual void EnterMenu()
    {
        terminal.ChangeCaretColor(Transparent);
        SetCurrentItem(0);
        SetPage(0);
        terminal.screenText.DeactivateInputField();
        terminal.screenText.interactable = false;
        LoadMenuNode();
    }

    /// <summary>
    /// Base method for exiting the InteractiveTerminalMenu
    /// </summary>
    /// <param name="enableInput">Determined whether to enable typing on the terminal again</param>
    public virtual void ExitMenu(bool enableInput)
    {
        terminal.ResetCaretColor();
        terminal.LoadNewNode(terminal.terminalNodes.specialNodes[1]);
        
        if (enableInput)
        {
            terminal.screenText.ActivateInputField();
            terminal.screenText.interactable = true;
        }

        enabled = false;

    }

    /// <summary>
    /// Update this InteractiveTerminalMenu's page size
    /// </summary>
    /// <param name="pageSize">The amount of items you wish to display per menu page. Default is 8</param>
    public void SetPageSize(int pageSize)
    {
        _pageSize = pageSize;
    }

    /// <summary>
    /// Update the current page number and reload the menu node.
    /// </summary>
    /// <param name="_requestedPage">The requested page number</param>
    /// <remarks>
    /// If requesting to change to the current page, this will do nothing. 
    /// By default, if the _requestedPage is not possible the value will cycle in <see cref="GetPageText"/>
    /// </remarks>
    public void SetPage(int _requestedPage)
    {
        if (_currentPage == _requestedPage) return;
        _currentPage = _requestedPage;
        LoadMenuNode();
    }

    /// <summary>
    /// Returns the value of _currentPage
    /// </summary>
    public int GetCurrentPage => _currentPage;
    /// <summary>
    /// Returns the value of _activeIndex
    /// </summary>
    public int GetActiveIndex => _activeIndex;

    /// <summary>
    /// Update the item index and reload the menu node.
    /// </summary>
    /// <param name="_requestedIndex">The index you are changing to</param>
    /// <remarks>
    /// If requesting to set this to the active index, this will do nothing. 
    /// By default, if the _requestedIndex is not possible the value will cycle in <see cref="GetPageText"/>
    /// </remarks>
    public void SetCurrentItem(int _requestedIndex)
    {
        if(_activeIndex == _requestedIndex) return;
        _activeIndex = _requestedIndex;
        LoadMenuNode();
    }

    /// <summary>
    /// Refresh the menu node with the latest text updates
    /// </summary>
    public virtual void LoadMenuNode()
    {
        if (!enabled || terminal == null || MenuNode == null || RootMenuItem == null)
            return;

        terminal.LoadNewNode(MenuNode);
    }

    public virtual void SelectActiveIndex()
    {
        if (ActiveMenuItem == null)
        {
            DawnPlugin.Logger.LogWarning("Unable to select from null ActiveMenuItem!");
            return;
        }

        if (DisplayMenuItemsOfType.Count == 0)
            return;

        DisplayMenuItemsOfType[_activeIndex].OnSelected();
    }

    /// <summary>
    /// Set the active menu item with this method.
    /// </summary>
    /// <param name="menuItem">The menu item you wish to promote to the active menu item</param>
    public void SetActiveMenuItem(TerminalMenuItem menuItem)
    {
        ActiveMenuItem = menuItem;
    }

    /// <summary>
    /// This method will generate the formatted menu text based on the active menu item, current page, current index, etc.
    /// </summary>
    /// <returns>Formatted string used for the interactive terminal menu</returns>
    public virtual string GetPageText()
    {
        StringBuilder message = new();

        if(ActiveMenuItem == null)
        {
            DawnPlugin.Logger.LogWarning($"ActiveMenuItem for TerminalMenu - {MenuName} is null!");
            message.AppendLine("Unable to get an active menu item :(\n\n\n");
            return message.ToString();
        }

        message.Append(ActiveMenuItem.HeaderText?.Provide());

        DisplayMenuItemsOfType = ActiveMenuItem.MenuItemContents.Provide();

        if (DisplayMenuItemsOfType.Count == 0)
        {
            message.Append($"\n\nThis menu listing is currently empty :(\n");
            message.Append(ActiveMenuItem.FooterText?.Provide());
            return message.ToString();
        }

        _currentPage = _currentPage.Cycle(1, Mathf.CeilToInt((float)DisplayMenuItemsOfType.Count / _pageSize));
        int startIndex = (_currentPage - 1) * _pageSize;
        int endIndex = Mathf.Min(startIndex + _pageSize, DisplayMenuItemsOfType.Count);
        _endIndex = endIndex;
        _activeIndex = _activeIndex.Cycle(startIndex, endIndex - 1);
        DawnPlugin.Logger.LogDebug($"{MenuName} menu activeselection: {_activeIndex}");

        for (int i = startIndex; i < endIndex; i++)
        {
            // set active selection prefix, if this is the active index
            string menuItem = (_activeIndex == i) ? ActiveSelectionPrefix : string.Empty;
            
            // Iprovider will have any special prefix/suffix/translation logic for the item name
            menuItem += DisplayMenuItemsOfType[i].Name.Provide();

            // set active selection suffix and invoke hover event if not null
            if (_activeIndex == i)
            {
                menuItem += ActiveSelectionSuffix;
                // allow inheriting classes to run a method on hover (active selection but not selected)
                DisplayMenuItemsOfType[i].OnHover();
            }

            // allow for inheriting classes to run a method on item shown in list
            DisplayMenuItemsOfType[i].OnShownInList();
            message.Append(menuItem + "\n");
        }

        int emptySpace = (endIndex - startIndex - _pageSize);

        if (emptySpace < 0)
        {
            for (int i = emptySpace; i < 0; i++)
                message.Append('\n');
        }

        message.Append(ActiveMenuItem.FooterText?.Provide());

        return message.ToString();
    }

    public override string ToString() => MenuName;

}
