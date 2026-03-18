
using System.Collections.Generic;

namespace Dawn;
public abstract class TerminalMenuItem(InteractiveTerminalMenu terminalMenu, IProvider<string> name, IProvider<List<TerminalMenuItem>> menuItemContents)
{
    public InteractiveTerminalMenu TerminalMenu = terminalMenu;
    public IProvider<string> Name = name;
    public IProvider<List<TerminalMenuItem>> MenuItemContents = menuItemContents;

    public IProvider<string>? HeaderText;
    public IProvider<string>? FooterText;

    public TerminalMenuItem? PreviousMenuItem;
    
    public TerminalMenuItem SetHeaderTextProvider(IProvider<string> headerProvider)
    {
        HeaderText = headerProvider;
        return this;
    }

    public TerminalMenuItem SetFooterTextProvider(IProvider<string> footerProvider)
    {
        FooterText = footerProvider;
        return this;
    }

    /// <summary>
    /// Method runs when this menu item is selected as the active selection.
    /// Base implementation will update the ActiveMenuItem for the terminal menu to this menu item and cache the previous menu item
    /// </summary>
    public virtual void OnSelected()
    {
        PreviousMenuItem = TerminalMenu.GetActiveMenuItem;
        TerminalMenu.SetActiveMenuItem(this);
        TerminalMenu.LoadMenuNode();
    }

    /// <summary>
    /// This runs when this menu item (as the active selection) is exited.
    /// Base implementation will either return you to the cached PreviousMenuItem or exit you from the menu (and load a standard terminal node)
    /// </summary>
    public virtual void OnExitThis()
    {
        if(PreviousMenuItem != null)
        {
            TerminalMenu.SetActiveMenuItem(PreviousMenuItem);
            TerminalMenu.LoadMenuNode();
        }
        else
        {
            TerminalMenu.ExitMenu(true);
        }
    }

    /// <summary>
    /// Method runs when this menu item is the active selection (but not selected)
    /// </summary>
    public virtual void OnHover() { }
    /// <summary>
    /// Method runs when this menu item is shown in the current page as a selectable item
    /// </summary>
    public virtual void OnShownInList() { }


    public override string ToString() => Name.Provide();
}
