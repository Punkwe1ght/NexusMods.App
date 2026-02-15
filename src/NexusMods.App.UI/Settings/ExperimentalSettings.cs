using System.Text.Json.Serialization;
using JetBrains.Annotations;
using NexusMods.Sdk.Settings;
using NexusMods.Sdk;
using NexusMods.Sdk.Games;

namespace NexusMods.App.UI.Settings;

/// <summary>
/// Settings that give access to experimental features in the UI.
/// </summary>
public record ExperimentalSettings : ISettings
{
    /// <summary>
    /// Shows all registered games, including those not yet fully supported.
    /// </summary>
    public bool EnableAllGames { get; [UsedImplicitly] set; } = ApplicationConstants.IsDebug;

    /// <summary>
    /// Enables uploading collections to Nexus Mods. Removed at general availability.
    /// </summary>
    // TODO: remove for GA
    public bool EnableCollectionSharing { get; [UsedImplicitly] set; }

    /// <summary>
    /// Games shown by default without enabling experimental mode.
    /// Not serialized; this is a compile-time constant.
    /// </summary>
    [JsonIgnore]
    public static IReadOnlyList<GameId> SupportedGames { get; } =
    [
        GameId.From("StardewValley"),
        GameId.From("RedEngine.Cyberpunk2077"),
        GameId.From("CreationEngine.FalloutNV"),
        GameId.From("CreationEngine.SkyrimSE"),
        GameId.From("CreationEngine.Fallout4"),
    ];

    public static ISettingsBuilder Configure(ISettingsBuilder settingsBuilder)
    {
        return settingsBuilder
            .ConfigureBackend(StorageBackendOptions.Use(StorageBackends.Json))
            .ConfigureProperty(
                x => x.EnableAllGames,
                new PropertyOptions<ExperimentalSettings, bool>
                {
                    Section = Sections.Experimental,
                    DisplayName = "Enable unsupported games",
                    DescriptionFactory = _ => "Manage games not yet fully supported.",
                    RequiresRestart = true,
                },
                new BooleanContainerOptions()
            )
            .ConfigureProperty(
                x => x.EnableCollectionSharing,
                new PropertyOptions<ExperimentalSettings, bool>
                {
                    Section = Sections.Experimental,
                    DisplayName = "Enable sharing collections",
                    DescriptionFactory = _ => "Upload collections to Nexus Mods.",
                },
                new BooleanContainerOptions()
            );
    }
}
