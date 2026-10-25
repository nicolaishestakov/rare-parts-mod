using UnityEngine;

namespace RareParts.Extensions;

internal static class KeyCodeExtensions
{
    public static string ToShortcutString(this KeyCode key) => key switch
    {
        KeyCode.Keypad0 => "Gray0",
        KeyCode.Keypad1 => "Gray1",
        KeyCode.Keypad2 => "Gray2",
        KeyCode.Keypad3 => "Gray3",
        KeyCode.Keypad4 => "Gray4",
        KeyCode.Keypad5 => "Gray5",
        KeyCode.Keypad6 => "Gray6",
        KeyCode.Keypad7 => "Gray7",
        KeyCode.Keypad8 => "Gray8",
        KeyCode.Keypad9 => "Gray9",
        KeyCode.KeypadPeriod => "Gray.",
        KeyCode.KeypadDivide => "Gray/",
        KeyCode.KeypadMultiply => "Gray*",
        KeyCode.KeypadMinus => "Gray-",
        KeyCode.KeypadPlus => "Gray+",
        KeyCode.KeypadEnter => "GrayEnter",
        KeyCode.KeypadEquals => "Gray=",
        KeyCode.Alpha0 => "0",
        KeyCode.Alpha1 => "1",
        KeyCode.Alpha2 => "2",
        KeyCode.Alpha3 => "3",
        KeyCode.Alpha4 => "4",
        KeyCode.Alpha5 => "5",
        KeyCode.Alpha6 => "6",
        KeyCode.Alpha7 => "7",
        KeyCode.Alpha8 => "8",
        KeyCode.Alpha9 => "9",
        KeyCode.Minus => "-",
        KeyCode.Equals => "=",
        KeyCode.Plus => "+",
        KeyCode.Asterisk => "*",
        KeyCode.Slash => "/",
        KeyCode.Backslash => "\\",
        KeyCode.Period => ".",
        KeyCode.Comma => ",",
        KeyCode.Semicolon => ";",
        KeyCode.Quote => "'",
        KeyCode.BackQuote => "`",
        KeyCode.LeftBracket => "[",
        KeyCode.RightBracket => "]",
        KeyCode.Return => "Enter",
        KeyCode.UpArrow => "Up",
        KeyCode.DownArrow => "Down",
        KeyCode.LeftArrow => "Left",
        KeyCode.RightArrow => "Right",
        _ => key.ToString()
    };
}
