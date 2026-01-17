using BepInEx.Configuration;

namespace MinitrainHobbyist.Settings;

public class MinitrainHobbyistSettings(ConfigFile config)
{
    public ConfigEntry<bool> CanFoolMikkelWithReplica = config.Bind<bool>("Mikkel", "AcceptReplica", true, "Whether Mikkel should be fooled by a Fine/Excellent quality Osmo Olut replica for his quest.");
    public ConfigEntry<bool> MikkelBuysMiniMinis = config.Bind<bool>("Mikkel", "BuyMiniMinitrains", true, "Whether Mikkel will buy minitrain miniatures off the player at market price after finishing his quest.");
}
