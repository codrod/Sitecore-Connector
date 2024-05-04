using Brightcove.Core.Exceptions;
using Brightcove.Core.Models;
using Brightcove.Core.Services;
using Brightcove.DataExchangeFramework.Settings;
using Sitecore.Data.Fields;
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
using Brightcove.Core.Extensions;
using Brightcove.DataExchangeFramework.Helpers;
using Sitecore.DataExchange.Providers.Sc.Plugins;
using Sitecore.Data;
using Sitecore.Globalization;
using Brightcove.DataExchangeFramework.Extensions;

namespace Brightcove.DataExchangeFramework.Processors
{
    public class UpdateVideoModelPipelineStepProcessor : BasePipelineStepProcessor
    {
        BrightcoveService service;

        protected override void ProcessPipelineStepInternal(PipelineStep pipelineStep = null, PipelineContext pipelineContext = null, ILogger logger = null)
        {
            var mappingSettings = GetPluginOrFail<MappingSettings>();
            var endpointSettings = GetPluginOrFail<BrightcoveEndpointSettings>();
            var webApiSettings = GetPluginOrFail<WebApiSettings>(endpointSettings.BrightcoveEndpoint);

            service = new BrightcoveService(webApiSettings.AccountId, webApiSettings.ClientId, webApiSettings.ClientSecret);
            Video video = (Video)pipelineContext.GetObjectFromPipelineContext(mappingSettings.TargetObjectLocation);
            ItemModel itemModel = (ItemModel)pipelineContext.GetObjectFromPipelineContext(mappingSettings.SourceObjectLocation);

            if (DeleteVideo(video, itemModel))
            {
                return;
            }

            ApplyMappings(mappingSettings.ModelMappingSets, itemModel, video);
            //video.Variants = ApplyMappingsForVariants(itemModelRepository, mappingSettings.VariantMappingSets, itemModel, video);

            LogDebug($"Updated the brightcove video model '{video.Id}'");
            UpdateVideo(video);
            UpdateFolder(video, itemModel);
        }

        private void ApplyMappings(IEnumerable<IMappingSet> mappingSets, ItemModel item, object model)
        {
            foreach (IMappingSet mappingSet in mappingSets)
            {
                var mappingContext = Mapper.ApplyMapping(mappingSet, item, model);

                if (Mapper.HasErrors(mappingContext))
                {
                    LogError($"Failed to apply mapping to the model '{item.GetItemId()}': {Mapper.GetFailedMappings(mappingContext)}");
                }
                else
                {
                    LogDebug($"Applied mapping to the model '{item.GetItemId()}'");
                }
            }
        }

        public bool DeleteVideo(Video video, ItemModel item)
        {
            //The item has been marked for deletion in Sitecore
            if ((string)item["Delete"] == "1")
            {
                LogInfo($"Deleting the brightcove model '{video.Id}' because it has been marked for deletion in Sitecore");
                service.DeleteVideo(video.Id);

                LogInfo($"Deleting the brightcove item '{item.GetItemId()}' because it has been marked for deleteion in Sitecore");
                itemModelRepository.Delete(item.GetItemId());

                return true;
            }

            return false;
        }

        public void UpdateVideo(Video video)
        {
            try
            {
                service.UpdateVideo(video);
            }
            //This is hacky fix to ignore invalid custom fields
            //This should be removed when a more permant solution is found
            catch (HttpStatusException ex)
            {
                if ((int)ex.Response.StatusCode != 422)
                    throw ex;

                string message = ex.Response.Content.ReadAsString();

                if (!message.Contains("custom_fields"))
                    throw ex;

                LogWarn($"The video model (or one of its variants) {video.Id} contains invalid custom fields so the custom fields will not be updated. Please verify all of the custom fields have been defined properly.");

                //Rerun with the invalid custom fields removed so the rest of the updates are made
                video.CustomFields = null;
                video.Variants = null;
                UpdateVideo(video);
                return;
            }
        }

        public void UpdateFolder(Video video, ItemModel item)
        {
            string folderField = (string)item["BrightcoveFolder"];

            if(string.IsNullOrWhiteSpace(folderField))
            {
                if(!string.IsNullOrWhiteSpace(video.Folder))
                {
                    service.RemoveFromFolder(video, video.Folder);
                    LogInfo($"Removed the video '{video.Id}' from the folder '{video.Folder}'");
                }
            }
            else
            {
                string folderId = (string)(itemModelRepository.Get(new Guid(folderField))["ID"]);

                if(video.Folder != folderId)
                {
                    service.MoveToFolder(video, folderId);
                    LogInfo($"Moved the video '{video.Id}' into the folder '{folderId}'");
                }
            }
        }

        public List<VideoVariant> ApplyMappingsForVariants(IItemModelRepository itemModelRepository, IEnumerable<IMappingSet> mappingSets, ItemModel item, Video model)
        {
            var variantItems = itemModelRepository.GetChildren(item.GetItemId(), item.GetLanguage());
            var variantModels = new List<VideoVariant>();

            foreach(ItemModel variantItem in variantItems)
            {
                VideoVariant variantModel = new VideoVariant()
                {
                    Id = model.Id
                };

                if((string)variantItem["Delete"] == "1")
                {
                    LogInfo($"Deleting the item '{variantItem.GetItemId()}' because it has been marked for deletion in Sitecore");
                    itemModelRepository.Delete(variantItem.GetItemId());
                    continue;
                }

                ApplyMappings(mappingSets, variantItem, variantModel);
                variantModel.Language = null;

                variantModels.Add(variantModel);
            }

            return variantModels;
        }

        /*
        public bool ResolveVideoVariant(VideoVariant videoVariant, ItemModel item)
        {
            //If variant is new then continue
            if(string.IsNullOrWhiteSpace((string)item["LastSyncTime"]))
            {
                return true;
            }

            if(!service.TryGetVideoVariant(videoVariant.Id, videoVariant.Language, out _))
            {
                itemModelRepository.Delete(item.GetItemId());
                LogWarn($"Deleting the item '{item.GetItemId()}' because it could not be resolved to the model '{videoVariant.Id}:{videoVariant.Language}'");
                return false;
            }

            return true;
        }

        public void CreateVideoVariant(VideoVariant videoVariant, ItemModel item)
        {
            service.CreateVideoVariant(videoVariant.Id, videoVariant.Name, videoVariant.Language);

            item["LastSyncTime"] = DateTime.UtcNow.ToString();
            itemModelRepository.Update(item.GetItemId(), item);
        }

        public void UpdateVideoVariant(VideoVariant videoVariant)
        {
            try
            {
                service.UpdateVideoVariant(videoVariant);
            }
            //This is hacky fix to ignore invalid custom fields
            //This should be removed when a more permant solution is found
            catch (HttpStatusException ex)
            {
                if ((int)ex.Response.StatusCode != 422)
                    throw ex;

                string message = ex.Response.Content.ReadAsString();

                if (!message.Contains("custom_fields"))
                    throw ex;

                LogWarn($"The video variant model {videoVariant.Id} contains invalid custom fields so the custom fields will not be updated. Please verify all of the custom fields have been defined properly.");

                //Rerun with the invalid custom fields removed so the rest of the updates are made
                videoVariant.CustomFields = null;
                UpdateVideoVariant(videoVariant);
                return;
            }
        }

        public bool DeleteVideoVariant(VideoVariant videoVariant, ItemModel itemModel)
        {
            //The item has been marked for deletion in Sitecore
            if ((string)itemModel["Delete"] == "1")
            {
                LogInfo($"Deleting the variant '{videoVariant.Id}:{videoVariant.Language}' because it has been marked for deletion in Sitecore");
                service.DeleteVideoVariant(videoVariant.Id, videoVariant.Language);

                LogInfo($"Deleting the item '{itemModel.GetItemId()}' because it has been marked for deletion in Sitecore");
                itemModelRepository.Delete(itemModel.GetItemId());

                return true;
            }

            return false;
        }
        */
    }
}
