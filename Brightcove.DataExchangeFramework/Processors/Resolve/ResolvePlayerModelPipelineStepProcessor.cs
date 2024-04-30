using Brightcove.Core.Models;
using Brightcove.Core.Services;
using Brightcove.DataExchangeFramework.Extensions;
using Brightcove.DataExchangeFramework.Settings;
using Sitecore.Data;
using Sitecore.Data.Items;
using Sitecore.DataExchange.Contexts;
using Sitecore.DataExchange.Extensions;
using Sitecore.DataExchange.Models;
using Sitecore.Globalization;
using Sitecore.Services.Core.Diagnostics;
using Sitecore.Services.Core.Model;
using System;

namespace Brightcove.DataExchangeFramework.Processors
{
    public class ResolvePlayerModelPipelineStepProcessor : BasePipelineStepWithWebApiEndpointProcessor
    {
        protected override void ProcessPipelineStepInternal(PipelineStep pipelineStep = null, PipelineContext pipelineContext = null, ILogger logger = null)
        {
            try
            {
                ResolveAssetModelSettings resolveAssetModelSettings = GetPluginOrFail<ResolveAssetModelSettings>();
                ItemModel item = (ItemModel)pipelineContext.GetObjectFromPipelineContext(resolveAssetModelSettings.AssetItemLocation);
                string playerId = (string)item["ID"];
                Player player;

                if (string.IsNullOrWhiteSpace(playerId))
                {
                    LogInfo($"Creating brightcove model for the brightcove item '{item.GetItemId()}'");
                    player = CreatePlayer(item);
                    pipelineContext.SetObjectOnPipelineContext(resolveAssetModelSettings.AssetModelLocation, player);
                }
                else if (service.TryGetPlayer(playerId, out player))
                {
                    pipelineContext.SetObjectOnPipelineContext(resolveAssetModelSettings.AssetModelLocation, player);
                    LogDebug($"Resolved the brightcove item '{item.GetItemId()}' to the brightcove model '{playerId}'");
                }
                else
                {
                    //The item was probably deleted or the ID has been modified incorrectly so we delete the item
                    LogWarn($"Deleting the brightcove item '{item.GetItemId()}' because the corresponding brightcove model '{playerId}' could not be found");
                    itemModelRepository.Delete(item.GetItemId());
                    pipelineContext.Finished = true;
                }
            }
            catch (Exception ex)
            {
                LogError($"Failed to resolve the brightcove item because an unexpected error has occured", ex);
                pipelineContext.Finished = true;
            }
        }

        private Player CreatePlayer(ItemModel itemModel)
        {
            Player player = service.CreatePlayer((string)itemModel["Name"], (string)itemModel["ShortDescription"]);

            itemModel["ID"] = player.Id;

            itemModelRepository.Update(itemModel.GetItemId(), itemModel);

            return player;
        }
    }
}
