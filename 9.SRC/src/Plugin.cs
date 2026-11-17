using System.Reflection;
using MegaCrit.Sts2.Core.Logging;
using MegaCrit.Sts2.Core.Modding;
using STS2RitsuLib;
using STS2RitsuLib.Interop;

namespace CharacterModIsolation;

[ModInitializer(nameof(Initialize))]
public static class Plugin
{
    public const string ModId = "CharacterModIsolation";

    public static Logger Logger { get; private set; } = null!;

    public static void Initialize()
    {
        Logger = RitsuLibFramework.CreateLogger(ModId);
        Logger.Info($"[init] CharacterModIsolation {typeof(Plugin).Assembly.GetName().Version}");

        try
        {
            ModTypeDiscoveryHub.RegisterModAssembly(ModId, Assembly.GetExecutingAssembly());
        }
        catch (Exception ex)
        {
            Logger.Warn($"[init] assembly registration failed: {ex.Message}");
        }

        try
        {
            ModSettings.RegisterData();
        }
        catch (Exception ex)
        {
            Logger.Error($"[init] settings data registration failed: {ex}");
        }

        try
        {
            RitsuLibFramework.SubscribeLifecycleOnce<MainMenuReadyEvent>(_ => OnMainMenuReady());
        }
        catch (Exception ex)
        {
            Logger.Error($"[init] lifecycle subscription failed: {ex}");
        }
    }

    private static void OnMainMenuReady()
    {
        try
        {
            PatchRegistry.InstallAll();
        }
        catch (Exception ex)
        {
            Logger.Error($"[init] patch installation failed: {ex}");
        }

        try
        {
            ModSettings.RegisterPage();
        }
        catch (Exception ex)
        {
            Logger.Error($"[init] settings page registration failed: {ex}");
        }

        try
        {
            CharacterModRegistry.PrewarmAuto();
        }
        catch (Exception ex)
        {
            Logger.Warn($"[init] auto prewarm failed: {ex.Message}");
        }

        Logger.Info("[init] main menu ready");
    }
}
