﻿using JosephM.Core.Extentions;
using JosephM.Core.Log;
using JosephM.Deployment.DataImport;
using JosephM.Record.IService;
using JosephM.Record.Xrm.XrmRecord;
using JosephM.Xrm;
using JosephM.Xrm.Schema;
using Microsoft.Xrm.Sdk;
using System;
using System.Collections.Generic;
using System.Linq;

namespace JosephM.Deployment.SpreadsheetImport
{
    public class SpreadsheetImportService
    {
        public SpreadsheetImportService(XrmRecordService xrmRecordService)
        {
            XrmRecordService = xrmRecordService;
        }

        public XrmRecordService XrmRecordService { get; }

        public SpreadsheetImportResponse DoImport(Dictionary<IMapSpreadsheetImport, IEnumerable<IRecord>> mappings, bool maskEmails, bool matchByName, LogController controller, bool useAmericanDates = false)
        {
            var response = new SpreadsheetImportResponse();

            var entitiesToImport = new List<Entity>();
            foreach (var mapping in mappings)
            {
                entitiesToImport.AddRange(MapToEntities(mapping.Value, mapping.Key, response, useAmericanDates));
            }
            PopulateEmptyNameFields(entitiesToImport);

            var dataImportService = new DataImportService(XrmRecordService);
            response.LoadDataImport(dataImportService.DoImport(entitiesToImport, controller, maskEmails, matchExistingRecords: matchByName));

            return response;
        }

        private IEnumerable<Entity> MapToEntities(IEnumerable<IRecord> queryRows, IMapSpreadsheetImport mapping, SpreadsheetImportResponse response, bool useAmericanDates)
        {
            var result = new List<Entity>();

            var nNRelationshipEntityNames = XrmRecordService
                .GetManyToManyRelationships()
                .Select(m => m.IntersectEntityName)
                .ToArray();

            var rowNumber = 0;
            foreach (var row in queryRows)
            {
                rowNumber++;
                try
                {
                    var targetType = mapping.TargetType;
                    var isNnRelation = nNRelationshipEntityNames.Contains(targetType);
                    var entity = new Entity(targetType);
                    var keyColumns = new string[0];

                    foreach (var fieldMapping in mapping.FieldMappings)
                    {
                        var targetField = fieldMapping.TargetField;
                        if (fieldMapping.TargetField != null)
                        {
                            var stringValue = row.GetStringField(fieldMapping.SourceField);
                            if (stringValue != null)
                                stringValue = stringValue.Trim();
                            if (isNnRelation)
                            {
                                //bit of hack
                                //for csv relationships just set to a string and map it later
                                //as the referenced record may not be created yet
                                entity.SetField(targetField, stringValue);
                            }
                            else if (XrmRecordService.XrmService.IsLookup(targetField, targetType))
                            {
                                //for lookups am going to set to a empty guid and allow the import part to replace with a correct guid
                                if (!stringValue.IsNullOrWhiteSpace())
                                    entity.SetField(targetField,
                                        new EntityReference(XrmRecordService.XrmService.GetLookupTargetEntity(targetField, targetType),
                                            Guid.Empty)
                                        {
                                            Name = stringValue
                                        });
                            }
                            else
                            {
                                try
                                {
                                    entity.SetField(targetField, XrmRecordService.XrmService.ParseField(targetField, targetType, stringValue, useAmericanDates));
                                }
                                catch(Exception ex)
                                {
                                    response.AddResponseItem(new DataImportResponseItem(targetType, targetField, null, stringValue, "Error Parsing Field - " + ex.Message, ex));
                                }
                            }
                        }
                    }
                    //okay any which are exact dupolicates to previous ones lets ignore
                    if (result.Any(r => r.GetFieldsInEntity().All(f => XrmRecordService.FieldsEqual(r.GetField(f), entity.GetField(f)))))
                        continue;

                    result.Add(entity);
                }
                catch (Exception ex)
                {
                    //todo perhaps could add row number and source details etc.
                    response.AddResponseItem(new DataImportResponseItem("Mapping Error", ex));
                }
            }
            return result;
        }

        private void PopulateEmptyNameFields(IEnumerable<Entity> entities)
        {
            foreach (var contact in entities.Where(e => e.LogicalName == Entities.contact))
            {
                if (contact.Contains(Fields.contact_.fullname)
                    && !contact.Contains(Fields.contact_.firstname)
                    && !contact.Contains(Fields.contact_.lastname))
                {
                    //okay for these dudes lets split their name into first and last name somehow
                    var name = contact.GetStringField(Fields.contact_.fullname);
                    if (name != null)
                    {
                        name = name.Trim();
                        var lastSpaceIndex = name.LastIndexOf(" ");
                        if (lastSpaceIndex == -1)
                        {
                            contact.SetField(Fields.contact_.firstname, name);
                        }
                        else
                        {
                            contact.SetField(Fields.contact_.firstname, name.Substring(0, lastSpaceIndex));
                            contact.SetField(Fields.contact_.lastname, name.Substring(lastSpaceIndex + 1));
                        }
                    }
                }
                if (!contact.Contains(Fields.contact_.fullname)
                    && (contact.Contains(Fields.contact_.firstname)
                        || contact.Contains(Fields.contact_.lastname)))
                {
                    //okay for these dudes lets split their name into first and last name somehow
                    var name = contact.GetStringField(Fields.contact_.firstname) + " " + contact.GetStringField(Fields.contact_.lastname);
                    if (name != null)
                    {
                        name = name.Trim();
                        contact.SetField(Fields.contact_.fullname, name);
                    }
                }
            }
        }
    }
}