using Microsoft.Extensions.DependencyInjection;
using NexusMods.Abstractions.Cli;
using NexusMods.Abstractions.Library;
using NexusMods.Abstractions.Loadouts;
using NexusMods.Abstractions.NexusModsLibrary;
using NexusMods.Abstractions.NexusWebApi;
using NexusMods.Abstractions.NexusWebApi.Types;
using NexusMods.MnemonicDB.Abstractions;
using NexusMods.Networking.NexusWebApi;
using NexusMods.Paths;
using NexusMods.Sdk.Loadouts;
using NexusMods.Sdk.ProxyConsole;

namespace NexusMods.Collections;

internal static class Verbs
{
    internal static IServiceCollection AddCollectionVerbs(this IServiceCollection collection) =>
        collection
            .AddVerb(() => InstallCollection);

    [Verb("install-collection", "Installs a collection into the given loadout")]
    private static async Task<int> InstallCollection([Injected] IRenderer renderer,
        [Option("l", "loadout", "Loadout to install the collection into")] Loadout.ReadOnly loadout,
        [Option("s", "slug", "Collection slug")] string slug,
        [Option("r", "revision", "Collection revision")] int revision,
        [Injected] TemporaryFileManager temporaryFileManager,
        [Injected] ILibraryService libraryService,
        [Injected] NexusModsLibrary nexusModsLibrary,
        [Injected] IServiceProvider serviceProvider,
        [Injected] ILoginManager loginManager,
        [Injected] IConnection connection,
        [Injected] CollectionDownloader collectionDownloader,
        [Injected] CancellationToken token)
    {
        await using var destination = temporaryFileManager.CreateFile();
        var downloadJob = nexusModsLibrary.CreateCollectionDownloadJob(destination, CollectionSlug.From(slug), RevisionNumber.From((ulong)revision), token);
        
        var libraryFile = await libraryService.AddDownload(downloadJob);

        if (!libraryFile.TryGetAsNexusModsCollectionLibraryFile(out var collectionFile))
            throw new InvalidOperationException("The library file is not a NexusModsCollectionLibraryFile");

        var revisionMetadata = await nexusModsLibrary.GetOrAddCollectionRevision(collectionFile, CollectionSlug.From(slug), RevisionNumber.From((ulong)revision), token);

        if (loginManager.IsPremium)
        {
            await collectionDownloader.DownloadItems(revisionMetadata, itemType: CollectionDownloader.ItemType.Required, db: connection.Db,
                cancellationToken: token
            );
        }

        var items = CollectionDownloader.GetItems(revisionMetadata, CollectionDownloader.ItemType.Required);
        if (!CollectionDownloader.IsFullyDownloaded(items, db: connection.Db))
        {
            var missing = collectionDownloader.GetMissingDownloadLinks(revisionMetadata, db: connection.Db, itemType: CollectionDownloader.ItemType.Required);
            if (missing.Count > 0)
            {
                await renderer.TextLine($"{missing.Count} downloads require manual action:");
                await renderer.Table(["Name", "Uri"], missing.Select(t => new object[] { t.Download.Name, t.Uri.ToString() }));
                await renderer.TextLine("Download these files manually, then add them to the library and retry.");
            }
            else
            {
                await renderer.TextLine("Some downloads failed. Check the app logs for details.");
            }
            return 1;
        }

        await InstallCollectionJob.Create(serviceProvider, loadout, collectionFile, revisionMetadata, items);
        return 0;
    }
}
