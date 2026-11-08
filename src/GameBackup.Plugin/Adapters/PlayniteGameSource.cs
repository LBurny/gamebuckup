using System;
using System.Collections.Generic;
using System.Linq;
using GameBackup.Core;
using GameBackup.Core.Models;
using Playnite.SDK;

namespace GameBackup.Plugin.Adapters
{
    /// <summary>
    /// Converts Playnite's game model into host-agnostic descriptors.
    /// Must be called on the UI thread; the result is a detached snapshot.
    /// </summary>
    public sealed class PlayniteGameSource
    {
        private readonly IPlayniteAPI _api;

        public PlayniteGameSource(IPlayniteAPI api)
        {
            _api = api;
        }

        public IReadOnlyList<PlayniteGameEntry> Snapshot()
        {
            var entries = new List<PlayniteGameEntry>();

            foreach (var game in _api.Database.Games)
            {
                if (!game.IsInstalled)
                {
                    continue;
                }

                var sourceName = game.Source?.Name ?? string.Empty;

                // Playnite stores the store-native id here: Steam AppID,
                // Ubisoft/Uplay id, GOG product id, Epic catalog id.
                var storeId = game.GameId ?? string.Empty;

                var platform = game.Platforms?.FirstOrDefault()?.Name ?? string.Empty;

                var descriptor = GameDescriptorFactory.Create(
                    "playnite:" + game.Id,
                    game.Name,
                    game.InstallDirectory ?? string.Empty,
                    sourceName,
                    storeId,
                    platform,
                    game.IsInstalled);

                var coverPath = string.Empty;
                try
                {
                    if (!string.IsNullOrEmpty(game.CoverImage))
                    {
                        coverPath = _api.Database.GetFullFilePath(game.CoverImage);
                    }
                }
                catch
                {
                    coverPath = string.Empty;
                }

                entries.Add(new PlayniteGameEntry
                {
                    Descriptor = descriptor,
                    CoverPath = coverPath,
                });
            }

            return entries
                .OrderBy(x => x.Descriptor.Name, StringComparer.CurrentCultureIgnoreCase)
                .ToList();
        }
    }
}
