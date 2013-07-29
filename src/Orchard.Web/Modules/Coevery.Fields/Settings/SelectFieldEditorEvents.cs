﻿using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using System.Linq;
using Coevery.Fields.Records;
using Orchard.ContentManagement;
using Orchard.ContentManagement.MetaData;
using Orchard.ContentManagement.MetaData.Builders;
using Orchard.ContentManagement.MetaData.Models;
using Orchard.ContentManagement.ViewModels;
using Orchard.Core.Settings.Metadata.Records;
using Orchard.Data;
using Orchard.Localization;

namespace Coevery.Fields.Settings
{
    public class SelectFieldListModeEvents : FieldEditorEvents
    {
        private readonly IRepository<OptionItemRecord> _optionItemRepository;
        private readonly IRepository<ContentPartDefinitionRecord> _partDefinitionRepository;
        private ContentPartFieldDefinitionRecord _itemField;
        private readonly Localizer _t;

        public SelectFieldListModeEvents(
            IRepository<OptionItemRecord> optionItemRepository,
            IRepository<ContentPartDefinitionRecord> partDefinitionRepository)
        {
            _optionItemRepository = optionItemRepository;
            _partDefinitionRepository = partDefinitionRepository;
            _t = NullLocalizer.Instance;
            _itemField = null;
        }

        public override IEnumerable<TemplateViewModel> PartFieldEditor(ContentPartFieldDefinition definition)
        {
            if (definition.FieldDefinition.Name == "SelectField" ||
                definition.FieldDefinition.Name == "SelectFieldCreate")
            {
                var model = definition.Settings.GetModel<SelectFieldSettings>();
                yield return DefinitionTemplate(model);
            }
        }

        public override IEnumerable<TemplateViewModel> PartFieldEditorUpdate(ContentPartFieldDefinitionBuilder builder, IUpdateModel updateModel)
        {
            if (builder.FieldType != "SelectField")
            {
                yield break;
            }
            if (_itemField == null)
            {
                updateModel.AddModelError("Field", _t("The select field hasn't been created"));
                yield break;
            }

            var model = new SelectFieldSettings();

            if (updateModel.TryUpdateModel(model, "SelectFieldSettings", null, null))
            {
                var labels = (from i in _optionItemRepository.Table
                              where i.ContentPartFieldDefinitionRecord == _itemField
                              select new
                              {
                                  Label = i.Value,
                                  Default = i.IsDefault
                              }).ToArray();
                var itemCount = labels.Length;

                if (itemCount < 1 || model.SelectCount < 1 || model.DefaultValue < 0
                    || model.DefaultValue > itemCount || model.DisplayLines > itemCount || model.SelectCount > itemCount
                    || (model.DisplayOption == SelectionMode.DropDown && model.DefaultValue > model.DisplayLines)
                    || (model.DisplayOption == SelectionMode.Checkbox && (model.SelectCount == 1 || model.DisplayLines > 1))
                    || (model.DisplayOption == SelectionMode.Radiobutton && (model.SelectCount > 1 || model.DisplayLines <= 1)))
                {
                    updateModel.AddModelError("Settings", _t("The setting values have conflicts."));
                    yield break;
                }

                var labelCache = new StringBuilder();
                for (int index = 1; index <= itemCount; index++)
                {
                    if (labels[index].Default)
                    {
                        model.DefaultValue = index;
                    }
                    labelCache.Append(labels[index] + SelectFieldSettings.LabelSeperator[0]);
                }
                model.LabelsStr = labelCache.ToString();

                UpdateSettings(model, builder, "SelectFieldSettings");
                builder.WithSetting("SelectFieldSettings.DisplayLines", model.DisplayLines.ToString());
                builder.WithSetting("SelectFieldSettings.DisplayOption", model.DisplayOption.ToString());
                builder.WithSetting("SelectFieldSettings.LabelsStr", model.LabelsStr);
                builder.WithSetting("SelectFieldSettings.DefaultValue", model.DefaultValue.ToString());
                builder.WithSetting("SelectFieldSettings.SelectCount", model.SelectCount.ToString());
            }

            yield return DefinitionTemplate(model);
        }

        public override IEnumerable<TemplateViewModel> PartFieldEditorCreate(ContentPartFieldDefinitionBuilder builder, string typeName, IUpdateModel updateModel)
        {
            if (builder.FieldType != "SelectField")
            {
                yield break;
            }

            var model = new SelectFieldSettings();
            if (updateModel.TryUpdateModel(model, "SelectFieldSettings", null, null))
            {
                _itemField = _partDefinitionRepository.Table.Single(x => x.Name == typeName)
                    .ContentPartFieldDefinitionRecords.Single(x => x.Name == builder.Name);

                //Basic Validation, should be replaced later
                if (string.IsNullOrWhiteSpace(model.LabelsStr))
                {
                    updateModel.AddModelError("LabelsStr", _t("The LabelsStr is invalid."));
                    yield break;
                }

                var labels = model.LabelsStr.Split(SelectFieldSettings.LabelSeperator, StringSplitOptions.RemoveEmptyEntries);
                var itemCount = labels.Length;

                if (itemCount < 1 || model.SelectCount < 1 || model.DefaultValue < 0
                    || model.DefaultValue > itemCount || model.DisplayLines > itemCount || model.SelectCount > itemCount
                    || (model.DisplayOption == SelectionMode.DropDown && model.DefaultValue > model.DisplayLines)
                    || (model.DisplayOption == SelectionMode.Checkbox && (model.SelectCount == 1 || model.DisplayLines > 1))
                    || (model.DisplayOption == SelectionMode.Radiobutton && (model.SelectCount > 1 || model.DisplayLines <= 1)))
                {
                    updateModel.AddModelError("Settings", _t("The setting values have conflicts."));
                    yield break;
                }

                int idIndex = 0;
                foreach (var label in labels)
                {
                    idIndex++;
                    var option = new OptionItemRecord
                    {
                        Value = label,
                        ContentPartFieldDefinitionRecord = _itemField,
                        IsDefault = (idIndex == model.DefaultValue)
                    };
                    _optionItemRepository.Create(option);
                }
                UpdateSettings(model, builder, "SelectFieldSettings");
                builder.WithSetting("SelectFieldSettings.DisplayLines", model.DisplayLines.ToString());
                builder.WithSetting("SelectFieldSettings.DisplayOption", model.DisplayOption.ToString());
                builder.WithSetting("SelectFieldSettings.LabelsStr", model.LabelsStr);
                builder.WithSetting("SelectFieldSettings.DefaultValue", model.DefaultValue.ToString());
                builder.WithSetting("SelectFieldSettings.SelectCount", model.SelectCount.ToString());
            }

            yield return DefinitionTemplate(model);
        }
    }
}