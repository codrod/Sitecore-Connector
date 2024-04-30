using Brightcove.Core.Models;
using Brightcove.Core.Services;
using Brightcove.DataExchangeFramework.Settings;
using Sitecore.Data;
using Sitecore.Data.Items;
using Sitecore.DataExchange.Attributes;
using Sitecore.DataExchange.Contexts;
using Sitecore.DataExchange.DataAccess;
using Sitecore.DataExchange.Extensions;
using Sitecore.DataExchange.Models;
using Sitecore.DataExchange.Plugins;
using Sitecore.DataExchange.Processors.PipelineSteps;
using Sitecore.DataExchange.Repositories;
using Sitecore.Services.Core.Diagnostics;
using Sitecore.Services.Core.Model;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Brightcove.DataExchangeFramework.Processors
{
    public class ResolveVideoModelPipelineStepProcessor : BasePipelineStepWithWebApiEndpointProcessor
    {
        protected override void ProcessPipelineStepInternal(PipelineStep pipelineStep = null, PipelineContext pipelineContext = null, ILogger logger = null)
        {
            try
            {
                ResolveAssetModelSettings resolveAssetModelSettings = GetPluginOrFail<ResolveAssetModelSettings>();
                ItemModel item = (ItemModel)pipelineContext.GetObjectFromPipelineContext(resolveAssetModelSettings.AssetItemLocation);
                string videoId = (string)item["ID"];
                Video video;

                if (service.TryGetVideo(videoId, out video))
                {
                    LogDebug($"Resolved the brightcove item '{item.GetItemId()}' to the brightcove model '{video.Id}'");
                    pipelineContext.SetObjectOnPipelineContext(resolveAssetModelSettings.AssetModelLocation, video);
                }
                else
                {
                    //The item was probably deleted or the ID has been modified incorrectly so we delete the item
                    LogWarn($"Deleting the brightcove item '{item.GetItemId()}' because the corresponding brightcove model '{videoId}' could not be found");
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
    }
}
