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
    public class ResolveLabelModelPipelineStepProcessor : BasePipelineStepWithWebApiEndpointProcessor
    {
        protected override void ProcessPipelineStepInternal(PipelineStep pipelineStep = null, PipelineContext pipelineContext = null, ILogger logger = null)
        {
            try
            {
                ResolveAssetModelSettings resolveAssetModelSettings = GetPluginOrFail<ResolveAssetModelSettings>();
                ItemModel item = (ItemModel)pipelineContext.GetObjectFromPipelineContext(resolveAssetModelSettings.AssetItemLocation);
                string labelField = (string)item["Label"];
                string newPathField = (string)item["NewPath"];
                Label label;

                if (string.IsNullOrWhiteSpace(labelField))
                {
                    if (string.IsNullOrWhiteSpace(newPathField) || !Label.TryParse(newPathField, out _))
                    {
                        LogWarn($"The new label item '{item.GetItemId()}' does not have a valid path field set so it will be ignored");
                        return;
                    }

                    LogInfo($"Creating brightcove model for the brightcove item '{item.GetItemId()}'");
                    label = CreateLabel(newPathField, item);
                    pipelineContext.SetObjectOnPipelineContext(resolveAssetModelSettings.AssetModelLocation, label);
                }
                else if (service.TryGetLabel(labelField, out label))
                {
                    pipelineContext.SetObjectOnPipelineContext(resolveAssetModelSettings.AssetModelLocation, label);
                    LogDebug($"Resolved the brightcove item '{item.GetItemId()}' to the brightcove model '{labelField}'");
                }
                else
                {
                    //The item was probably deleted or the ID has been modified incorrectly so we delete the item
                    LogWarn($"Deleting the brightcove item '{item.GetItemId()}' because the corresponding brightcove model '{labelField}' could not be found");
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

        private Label CreateLabel(string labelPath, ItemModel itemModel)
        {
            Label label = service.CreateLabel(labelPath);

            itemModel["Label"] = label.Path;
            itemModel["NewPath"] = "";
            itemModel["ItemName"] = label.SitecoreName;
            itemModel["__Display name"] = label.Path;

            itemModelRepository.Update(itemModel.GetItemId(), itemModel);

            return label;
        }
    }
}
