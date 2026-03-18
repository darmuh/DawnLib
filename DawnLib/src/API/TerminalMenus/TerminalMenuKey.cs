using System;
using UnityEngine.InputSystem;

namespace Dawn;
public class TerminalMenuKey
{
    public InteractiveTerminalMenu TerminalMenu;
    public Key MenuKey;
    public Action KeyAction;

    public TerminalMenuKey(InteractiveTerminalMenu terminalMenu, Key menuKey, Action keyAction)
    {
        TerminalMenu = terminalMenu;
        MenuKey = menuKey;
        KeyAction = keyAction;
        terminalMenu.CheckInput.OnInvoke += UpdateInput;
    }

    public void UpdateInput()
    {
        if (Keyboard.current[MenuKey].isPressed)
            KeyAction?.Invoke();
    }
}
