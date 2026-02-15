using System.Collections.Immutable;
using DynamicData.Kernel;
using Microsoft.Extensions.DependencyInjection;
using NexusMods.Abstractions.Diagnostics.Emitters;
using NexusMods.Abstractions.Games;
using NexusMods.Abstractions.Library.Installers;
using NexusMods.Abstractions.Loadouts.Synchronizers;
using NexusMods.Games.CreationEngine.Abstractions;
using NexusMods.Games.CreationEngine.Emitters;
using NexusMods.Games.CreationEngine.FalloutNV.Emitters;
using NexusMods.Games.CreationEngine.FalloutNV.Installers;
using NexusMods.Games.CreationEngine.FalloutNV.SortOrder;
using NexusMods.Games.CreationEngine.Installers;
using NexusMods.Games.CreationEngine.Parsers;
using NexusMods.Games.FOMOD;
using NexusMods.Hashing.xxHash3;
using NexusMods.Paths;
using NexusMods.Sdk.FileStore;
using NexusMods.Sdk.Games;
using NexusMods.Sdk.IO;

namespace NexusMods.Games.CreationEngine.FalloutNV;

public class FalloutNV : ICreationEngineGame, IGameData<FalloutNV>
{
    private readonly IStreamSourceDispatcher _streamSource;

    public static GameId GameId { get; } = GameId.From("CreationEngine.FalloutNV");
    public static string DisplayName => "Fallout: New Vegas";
    public static Optional<Sdk.NexusModsApi.NexusModsGameId> NexusModsGameId =>
        Sdk.NexusModsApi.NexusModsGameId.From(130);

    public StoreIdentifiers StoreIdentifiers { get; } = new(GameId)
    {
        SteamAppIds = [22380u],
        GOGProductIds = [1454587428L],
    };

    public IStreamFactory IconImage { get; } =
        new EmbeddedResourceStreamFactory<FalloutNV>(
            "NexusMods.Games.CreationEngine.Resources.FalloutNV.thumbnail.webp");

    public IStreamFactory TileImage { get; } =
        new EmbeddedResourceStreamFactory<FalloutNV>(
            "NexusMods.Games.CreationEngine.Resources.FalloutNV.tile.webp");

    private readonly Lazy<ILoadoutSynchronizer> _synchronizer;
    public ILoadoutSynchronizer Synchronizer => _synchronizer.Value;
    public ILibraryItemInstaller[] LibraryItemInstallers { get; }
    private readonly Lazy<ISortOrderManager> _sortOrderManager;
    public ISortOrderManager SortOrderManager => _sortOrderManager.Value;
    public IDiagnosticEmitter[] DiagnosticEmitters { get; }

    public bool SupportsEsl => false;

    public FalloutNV(IServiceProvider provider)
    {
        _streamSource = provider.GetRequiredService<IStreamSourceDispatcher>();

        _synchronizer = new Lazy<ILoadoutSynchronizer>(
            () => new FalloutNVSynchronizer(provider, this));
        _sortOrderManager = new Lazy<ISortOrderManager>(() =>
        {
            var sortOrderManager = provider.GetRequiredService<SortOrderManager>();
            sortOrderManager.RegisterSortOrderVarieties(
                [provider.GetRequiredService<FnvPluginSortOrderVariety>()], this);
            return sortOrderManager;
        });

        DiagnosticEmitters =
        [
            new MissingMasterEmitter(this),
            provider.GetRequiredService<ArchiveInvalidationEmitter>(),
            new FourGbPatcherEmitter(),
            new PluginLimitEmitter(),
            new ModLimitFixEmitter(),
            new XnvseDetectionEmitter(),
            provider.GetRequiredService<IniConflictEmitter>(),
            provider.GetRequiredService<BsaLoadOrderEmitter>(),
            provider.GetRequiredService<NvseVersionMismatchEmitter>(),
            provider.GetRequiredService<ProtonRequirementsEmitter>(),
        ];

        LibraryItemInstallers =
        [
            FomodXmlInstaller.Create(provider, new GamePath(LocationId.Game, "Data")),
            provider.GetRequiredService<FnvModInstaller>(),
            new StopPatternInstaller(provider)
            {
                GameId = GameId,
                GameAliases = ["Fallout New Vegas", "FalloutNV", "FNV", "Fallout NV"],
                TopLevelDirs = KnownPaths.CommonTopLevelFolders,
                StopPatterns = ["(^|/)nvse(/|$)"],
                // FNV has no .esl support
                PluginLikeExtensions = [KnownCEExtensions.ESM, KnownCEExtensions.ESP],
                // FNV uses .bsa only, not .ba2
                ArchiveLikeExtensions = [KnownCEExtensions.BSA],
                EngineFiles =
                [
                    // xNVSE
                    @"nvse_loader\.exe",
                    @"nvse_.*\.dll",
                    @"nvse_steam_loader\.dll",
                    // 4GB Patcher
                    @"fnv4gb\.exe",
                    // Plugin Preloader
                    @"winhttp\.dll",
                    @"IpHlpAPI\.dll",
                ],
            }.Build(),
        ];
    }

    public ImmutableDictionary<LocationId, AbsolutePath> GetLocations(
        IFileSystem fileSystem, GameLocatorResult gameLocatorResult)
    {
        return new Dictionary<LocationId, AbsolutePath>
        {
            { LocationId.Game, gameLocatorResult.Path },
            { LocationId.AppData,
                fileSystem.GetKnownPath(KnownPath.LocalApplicationDataDirectory) / "FalloutNV" },
            { LocationId.Preferences,
                fileSystem.GetKnownPath(KnownPath.MyGamesDirectory) / "FalloutNV" },
        }.ToImmutableDictionary();
    }

    public GamePath GetPrimaryFile(GameInstallation installation) =>
        new(LocationId.Game, "FalloutNV.exe");

    public async ValueTask<IPluginInfo?> ParsePlugin(Hash hash, RelativePath? name = null)
    {
        await using var stream = await _streamSource.OpenAsync(hash);
        if (stream is null) return null;

        var header = Tes4HeaderParser.Parse(stream);
        if (header is null) return null;

        var fileName = name?.FileName.ToString() ?? "unknown.esm";
        return new Tes4PluginInfoAdapter(fileName, header);
    }

    public Optional<GamePath> GetFallbackCollectionInstallDirectory(GameInstallation installation)
    {
        return Optional<GamePath>.Create(new GamePath(LocationId.Game, "Data"));
    }

    public GamePath PluginsFile => FalloutNVKnownPaths.PluginsFile;
}
