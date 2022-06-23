﻿using Brightcove.Core.Exceptions;
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

namespace Brightcove.DataExchangeFramework.Processors
{
    public class UpdateVideoItemProcessor : BasePipelineStepProcessor
    {
        BrightcoveService service;
        IItemModelRepository itemModelRepository;

        protected override void ProcessPipelineStepInternal(PipelineStep pipelineStep = null, PipelineContext pipelineContext = null, ILogger logger = null)
        {
            var mappingSettings = GetPluginOrFail<MappingSettings>();
            var endpointSettings = GetPluginOrFail<BrightcoveEndpointSettings>();
            var webApiSettings = GetPluginOrFail<WebApiSettings>(endpointSettings.BrightcoveEndpoint);
            itemModelRepository = GetPluginOrFail<ItemModelRepositorySettings>(endpointSettings.SitecoreEndpoint).ItemModelRepository;
            service = new BrightcoveService(webApiSettings.AccountId, webApiSettings.ClientId, webApiSettings.ClientSecret);

            ItemModel item = null;
            Video model = null;

            try
            {
                item = (ItemModel)this.GetObjectFromPipelineContext(mappingSettings.TargetObjectLocation, pipelineContext, logger);
                model = (Video)this.GetObjectFromPipelineContext(mappingSettings.SourceObjectLocation, pipelineContext, logger);

                foreach (IMappingSet mappingSet in mappingSettings.ModelMappingSets)
                {
                    var mappingContext = Mapper.ApplyMapping(mappingSet, model, item);

                    if (Mapper.HasErrors(mappingContext))
                    {
                        LogError($"Failed to apply mapping to the item '{item.GetItemId()}': {Mapper.GetFailedMappings(mappingContext)}");
                    }
                    else
                    {
                        LogDebug($"Applied mapping to the item '{item.GetItemId()}'");
                    }
                }

                if (!ItemUpdater.Update(itemModelRepository, item))
                {
                    LogError($"Failed to update the item '{item.GetItemId()}'");
                }
                else
                {
                    LogDebug($"Updated the item '{item.GetItemId()}'");
                }

                UpdateVariants(mappingSettings.VariantMappingSets, item, model);
            }
            catch(Exception ex)
            {
                LogError($"An unexpected error occured updating the item '{item?.GetItemId()}'", ex);
            }
        }


        private void UpdateVariants(IEnumerable<IMappingSet> mappingSets, ItemModel videoItem, Video video)
        {
            var videoVariants = service.GetVideoVariants(video.Id);

            foreach(VideoVariant videoVariant in videoVariants)
            {
                ItemModel videoVariantItem = ResolveVideoVariant(videoItem, videoVariant);
                ApplyMappings(mappingSets, videoVariant, videoVariantItem);
                UpdateVideoVariant(videoVariantItem);
            }
        }

        private void ApplyMappings(IEnumerable<IMappingSet> mappingSets, object model, ItemModel item)
        {
            foreach (IMappingSet mappingSet in mappingSets)
            {
                var mappingContext = Mapper.ApplyMapping(mappingSet, model, item);

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

        private ItemModel ResolveVideoVariant(ItemModel videoItem, VideoVariant videoVariant)
        {
            ItemModel variantItem = itemModelRepository.GetChildren(videoItem.GetItemId()).Where(c => c["Language"].ToString() == videoVariant.Language).FirstOrDefault();

            if (variantItem == null)
            {
                Guid variantItemId = itemModelRepository.Create(ItemUtil.ProposeValidItemName(videoVariant.Name), new Guid("{A7EAF4FD-BCF3-4511-9E8C-2ED0B165F1D6}"), videoItem.GetItemId());
                variantItem = itemModelRepository.Get(variantItemId);
            }

            return variantItem;
        }

        private void UpdateVideoVariant(ItemModel item)
        {
            if (!ItemUpdater.Update(itemModelRepository, item))
            {
                LogError($"Failed to update the variant '{item.GetItemId()}'");
            }
            else
            {
                LogDebug($"Updated the variant '{item.GetItemId()}'");
            }
        }
    }
}
