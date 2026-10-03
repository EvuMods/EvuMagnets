#nullable disable
using System;
using System.Reflection;
using BepInEx.Configuration;

namespace EvuMagnets;

/// <summary>
/// Display hints for BepInEx ConfigurationManager and the forks that reflect this type by name.
/// Copied from the MIT-licensed template at https://github.com/BepInEx/BepInEx.ConfigurationManager
/// </summary>
#pragma warning disable 0169, 0414, 0649
internal sealed class ConfigurationManagerAttributes
{
    public bool? ShowRangeAsPercent;

    public Action<ConfigEntryBase> CustomDrawer;

    public CustomHotkeyDrawerFunc CustomHotkeyDrawer;

    public delegate void CustomHotkeyDrawerFunc(ConfigEntryBase setting, ref bool isCurrentlyAcceptingInput);

    public bool? Browsable;

    public string Category;

    public object DefaultValue;

    public bool? HideDefaultButton;

    public bool? HideSettingName;

    public string Description;

    public string DispName;

    public int? Order;

    public bool? ReadOnly;

    public bool? IsAdvanced;

    public Func<object, string> ObjToStr;

    public Func<string, object> StrToObj;
}
#pragma warning restore 0169, 0414, 0649
