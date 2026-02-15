using Microsoft.Extensions.DependencyInjection;
using NexusMods.Abstractions.Games;
using NexusMods.Abstractions.Loadouts;
using NexusMods.Games.CreationEngine.FalloutNV;
using NexusMods.Games.CreationEngine.FalloutNV.Emitters;
using NexusMods.Games.CreationEngine.FalloutNV.Installers;
using NexusMods.Games.CreationEngine.FalloutNV.Models;
using NexusMods.Sdk.Settings;

namespace NexusMods.Games.CreationEngine;

public static class Services
{
    public static IServiceCollection AddCreationEngine(this IServiceCollection services)
    {
        services.AddGame<SkyrimSE.SkyrimSE>();
        services.AddSingleton<ITool>(s => RunGameViaScriptExtenderTool<SkyrimSE.SkyrimSE>.Create(s, KnownPaths.SKSE64Loader));

        services.AddGame<Fallout4.Fallout4>();
        services.AddSingleton<ITool>(s => RunGameViaScriptExtenderTool<Fallout4.Fallout4>.Create(s, KnownPaths.F4SELoader));

        services.AddGame<FalloutNV.FalloutNV>();
        services.AddSingleton<ITool>(s => RunGameViaScriptExtenderTool<FalloutNV.FalloutNV>.Create(s, KnownPaths.NVSELoader));

        // FNV models
        services.AddNvsePluginLoadoutItemModel();
        services.AddIniTweakLoadoutFileModel();

        // FNV installer
        services.AddSingleton<FnvModInstaller>();

        // FNV diagnostics
        services.AddSingleton<ArchiveInvalidationEmitter>();
        services.AddSingleton<IniConflictEmitter>();
        services.AddSingleton<BsaLoadOrderEmitter>();
        services.AddSingleton<NvseVersionMismatchEmitter>();
        services.AddSingleton<PluginLimitEmitter>();
        services.AddSingleton<FourGbPatcherEmitter>();
        services.AddSingleton<XnvseDetectionEmitter>();
        services.AddSingleton<ModLimitFixEmitter>();
        services.AddSingleton<ProtonRequirementsEmitter>();

        // FNV settings
        services.AddSettings<FalloutNVSettings>();

        return services;
    }
}
