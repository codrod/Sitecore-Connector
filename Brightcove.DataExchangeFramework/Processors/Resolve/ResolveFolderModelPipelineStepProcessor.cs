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
    public class ResolveFolderModelPipelineStepProcessor : BasePipelineStepWithWebApiEndpointProcessor
    {
        protected override void ProcessPipelineStepInternal(PipelineStep pipelineStep = null, PipelineContext pipelineContext = null, ILogger logger = null)
        {
            try
            {
                ResolveAssetModelSettings resolveAssetModelSettings = GetPluginOrFail<ResolveAssetModelSettings>();
                ItemModel item = (ItemModel)pipelineContext.GetObjectFromPipelineContext(resolveAssetModelSettings.AssetItemLocation);
                string id = (string)item["ID"];
                Folder folder;

                if (string.IsNullOrWhiteSpace(id))
                {
                    LogInfo($"Creating brightcove model for the new brightcove item '{item.GetItemId()}'");
                    folder = CreateFolder(item);
                    pipelineContext.SetObjectOnPipelineContext(resolveAssetModelSettings.AssetModelLocation, folder);
                }
                else if (service.TryGetFolder(id, out folder))
                {
                    pipelineContext.SetObjectOnPipelineContext(resolveAssetModelSettings.AssetModelLocation, folder);
                    LogDebug($"Resolved the brightcove item '{item.GetItemId()}' to the brightcove model '{id}'");
                }
                else
                {
                    //The item was probably deleted or the ID has been modified incorrectly so we delete the item
                    LogWarn($"Deleting the brightcove item '{item.GetItemId()}' because the corresponding brightcove model '{id}' could not be found");
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

        private Folder CreateFolder(ItemModel itemModel)
        {
            Folder folder = service.CreateFolder((string)itemModel["Name"]);

            itemModel["ID"] = folder.Id;

            itemModelRepository.Update(itemModel.GetItemId(), itemModel);

            return folder;
        }
    }
}
