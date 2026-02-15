using NexusMods.Abstractions.Loadouts;
using NexusMods.MnemonicDB.Abstractions.Attributes;
using NexusMods.MnemonicDB.Abstractions.Models;

namespace NexusMods.Games.CreationEngine.FalloutNV.Models;

/// <summary>
/// Marks a loadout file as an INI tweak targeting a specific FNV INI file.
/// </summary>
[Include<LoadoutFile>]
public partial class IniTweakLoadoutFile : IModelDefinition
{
    private const string Namespace = "NexusMods.CreationEngine.FalloutNV.IniTweakLoadoutFile";

    /// <summary>
    /// Marker indicating this file is an INI tweak.
    /// </summary>
    public static readonly MarkerAttribute IniTweakFile =
        new(Namespace, nameof(IniTweakFile)) { IsIndexed = true };

    /// <summary>
    /// Which INI file this tweak targets (e.g., "Fallout.ini", "FalloutPrefs.ini", "FalloutCustom.ini").
    /// </summary>
    public static readonly StringAttribute TargetIniFile =
        new(Namespace, nameof(TargetIniFile));
}
