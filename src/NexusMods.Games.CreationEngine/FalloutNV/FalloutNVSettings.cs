using NexusMods.Sdk.Settings;

namespace NexusMods.Games.CreationEngine.FalloutNV;

public class FalloutNVSettings : ISettings
{
    /// <summary>
    /// If true, all game folders are backed up. Greatly increases disk usage.
    /// Should only be changed before managing the game.
    /// </summary>
    public bool DoFullGameBackup { get; set; } = false;

    /// <summary>
    /// If true, the archive invalidation emitter warns when bInvalidateOlderFiles is not set.
    /// </summary>
    public bool CheckArchiveInvalidation { get; set; } = true;

    public static ISettingsBuilder Configure(ISettingsBuilder settingsBuilder)
    {
        return settingsBuilder
            .ConfigureProperty(
                x => x.DoFullGameBackup,
                new PropertyOptions<FalloutNVSettings, bool>
                {
                    Section = Sections.Experimental,
                    DisplayName = "Full game backup: Fallout New Vegas",
                    DescriptionFactory = _ => "Backup all game folders. Greatly increases disk usage. Should only be changed before managing the game.",
                },
                new BooleanContainerOptions()
            )
            .ConfigureProperty(
                x => x.CheckArchiveInvalidation,
                new PropertyOptions<FalloutNVSettings, bool>
                {
                    Section = Sections.GameSpecific,
                    DisplayName = "Check archive invalidation: Fallout New Vegas",
                    DescriptionFactory = _ => "Warn when archive invalidation is not enabled in INI files.",
                },
                new BooleanContainerOptions()
            );
    }
}
