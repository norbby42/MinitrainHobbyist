using BepInEx;
using BepInEx.Logging;
using MinitrainHobbyist.Settings;
using Lavender;
using System.Reflection;
using System.IO;

namespace MinitrainHobbyist;

[BepInPlugin(LCMPluginInfo.PLUGIN_GUID, LCMPluginInfo.PLUGIN_NAME, LCMPluginInfo.PLUGIN_VERSION)]
public class MinitrainHobbyistPlugin : BaseUnityPlugin
{
    internal static ManualLogSource Log = null!;
    internal static MinitrainHobbyistSettings Settings = null!;

    private void Awake()
    {
        Log = Logger;

        Settings = new(Config);

        // Log our awake here so we can see it in LogOutput.txt file
        Log.LogMessage($"Plugin {LCMPluginInfo.PLUGIN_NAME} version {LCMPluginInfo.PLUGIN_VERSION} loading...");

        int itemsadded = Lavender.Lavender.AddCustomItemsFromJson(Path.Combine(Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location), "items.json"), LCMPluginInfo.PLUGIN_NAME);
        int recipesadded = Lavender.Lavender.AddCustomRecipesFromJson(Path.Combine(Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location), "recipes.json"), LCMPluginInfo.PLUGIN_NAME);

        Lavender.Lavender.AddConversationPatcher(new MikkelConversationPatcher());
    }

}
