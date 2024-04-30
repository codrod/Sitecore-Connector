using Brightcove.Core.Models;
using Brightcove.Core.Services;
using Brightcove.DataExchangeFramework.Extensions;
using Brightcove.DataExchangeFramework.Settings;
using Sitecore.Data;
using Sitecore.Data.Items;
using Sitecore.DataExchange.Contexts;
using Sitecore.DataExchange.Extensions;
using Sitecore.DataExchange.Models;
using Sitecore.DataExchange.Processors.PipelineSteps;
using Sitecore.Globalization;
using Sitecore.Services.Core.Diagnostics;
using Sitecore.Services.Core.Model;
using System;

namespace Brightcove.DataExchangeFramework.Processors
{
    public class ResolvePlaylistModelPipelineStepProcessor : BasePipelineStepWithWebApiEndpointProcessor
    {
        protected override void ProcessPipelineStepInternal(PipelineStep pipelineStep = null, PipelineContext pipelineContext = null, ILogger logger = null)
        {
            try
            {
                ResolveAssetModelSettings resolveAssetModelSettings = GetPluginOrFail<ResolveAssetModelSettings>();
                ItemModel item = (ItemModel)pipelineContext.GetObjectFromPipelineContext(resolveAssetModelSettings.AssetItemLocation);
                string playlistId = (string)item["ID"];
                PlayList playlist;

                if (string.IsNullOrWhiteSpace(playlistId))
                {
                    LogInfo($"Creating brightcove model for the brightcove item '{item.GetItemId()}'");
                    playlist = CreatePlaylist(item);
                    pipelineContext.SetObjectOnPipelineContext(resolveAssetModelSettings.AssetModelLocation, playlist);
                }
                else if (service.TryGetPlaylist(playlistId, out playlist))
                {
                    pipelineContext.SetObjectOnPipelineContext(resolveAssetModelSettings.AssetModelLocation, playlist);
                    LogDebug($"Resolved the brightcove item '{item.GetItemId()}' to the brightcove model '{playlistId}'");
                }
                else
                {
                    //The item was probably deleted or the ID has been modified incorrectly so we delete the item
                    LogWarn($"Deleting the brightcove item '{item.GetItemId()}' because the corresponding brightcove model '{playlistId}' could not be found");
                    itemModelRepository.Delete(item.GetItemId());
                    pipelineContext.Finished = true;
                }
            }
            catch(Exception ex)
            {
                LogError($"Failed to resolve the brightcove item because an unexpected error has occured", ex);
                pipelineContext.Finished = true;
            }
        }

        private PlayList CreatePlaylist(ItemModel itemModel)
        {
            PlayList playlist = service.CreatePlaylist((string)itemModel["Name"]);

            itemModel["ID"] = playlist.Id;

            itemModelRepository.Update(itemModel.GetItemId(), itemModel);

            return playlist;
        }
    }
}
