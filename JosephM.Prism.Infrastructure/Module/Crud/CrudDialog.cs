﻿using JosephM.Application.Desktop.Module.Crud.BulkCopyFieldValue;
using JosephM.Application.Desktop.Module.Crud.BulkDelete;
using JosephM.Application.Desktop.Module.Crud.BulkReplace;
using JosephM.Application.Desktop.Module.Crud.BulkUpdate;
using JosephM.Application.ViewModel.Dialog;
using JosephM.Application.ViewModel.Grid;
using JosephM.Application.ViewModel.Query;
using JosephM.Core.FieldType;
using JosephM.Record.Extentions;
using JosephM.Record.IService;
using JosephM.Record.Query;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace JosephM.Application.Desktop.Module.Crud
{
    public class CrudDialog : DialogViewModel
    {
        public IRecordService RecordService { get; set; }
        public QueryViewModel QueryViewModel { get; private set; }

        public CrudDialog(IDialogController dialogController, IRecordService recordService)
            : base(dialogController)
        {
            SetTabLabel("Crud/Query Data");
            RecordService = recordService;
            //this is so that the completion screen isnt loaded when the dialog completes
            //this dialog just loads the query into the ui and does not have a 'completion'
            OverideCompletionScreenMethod = () => { };
        }

        protected override void CompleteDialogExtention()
        {
            try
            {
                //this bit messy because may take a while to load the record types
                //so spawn on async thread, then back to the main thread for th ui objects
                LoadingViewModel.LoadingMessage = "Loading Types";
                var recordTypesForBrowsing = Task.Run<IEnumerable<string>>(() => RecordService.GetAllRecordTypes()
                    .Where(r =>
                    RecordService.GetRecordTypeMetadata(r).Searchable)
                    .Union(AdditionalExplicitTypes)
                    .ToArray()).Result;

                DoOnMainThread(() =>
                {
                    try
                    {
                        var customFunctionList = new List<CustomGridFunction>()
                        {
                            new CustomGridFunction("BULKUPDATE", "Bulk Update", new []
                            {
                                new CustomGridFunction("BULKUPDATESELECTED", "Selected Only", (g) =>
                                {
                                    TriggerBulkUpdate(true);
                                }, (g) => g.SelectedRows.Any()),
                                new CustomGridFunction("BULKUPDATEALL", "All Results", (g) =>
                                {
                                    TriggerBulkUpdate(false);
                                }, (g) => g.GridRecords != null && g.GridRecords.Any()),
                            }),
                            new CustomGridFunction("BULKREPLACE", "Bulk Replace", new []
                            {
                                new CustomGridFunction("BULKREPLACESELECTED", "Selected Only", (g) =>
                                {
                                    TriggerBulkReplace(true);
                                }, (g) => g.SelectedRows.Any()),
                                new CustomGridFunction("BULKREPLACEALL", "All Results", (g) =>
                                {
                                    TriggerBulkReplace(false);
                                }, (g) => g.GridRecords != null && g.GridRecords.Any()),
                            }),
                            new CustomGridFunction("BULKCOPYFIELDVALUE", "Bulk Copy Field Value", new []
                            {
                                new CustomGridFunction("BULKCOPYFIELDVALUESELECTED", "Selected Only", (g) =>
                                {
                                    TriggerBulkCopyFieldValue(true);
                                }, (g) => g.SelectedRows.Any()),
                                new CustomGridFunction("BULKCOPYFIELDVALUEALL", "All Results", (g) =>
                                {
                                    TriggerBulkCopyFieldValue(false);
                                }, (g) => g.GridRecords != null && g.GridRecords.Any()),
                            }),
                            new CustomGridFunction("DELETE", "Bulk Delete", new []
                            {
                                new CustomGridFunction("BULKDELETESELECTED", "Selected Only", (g) =>
                                {
                                    TriggerBulkDelete(true);
                                }, (g) => g.SelectedRows.Any()),
                                new CustomGridFunction("BULKDELETEALL", "All Results", (g) =>
                                {
                                    TriggerBulkDelete(false);
                                }, (g) => g.GridRecords != null && g.GridRecords.Any()),
                            })
                        };

                        QueryViewModel = new QueryViewModel(recordTypesForBrowsing, RecordService, ApplicationController, allowQuery: true, customFunctions: customFunctionList);
                        Controller.LoadToUi(QueryViewModel);
                    }
                    catch (Exception ex)
                    {
                        ApplicationController.ThrowException(ex);
                    }
                });
            }
            catch (Exception ex)
            {
                ApplicationController.ThrowException(ex);
            }
        }

        public virtual IEnumerable<string> AdditionalExplicitTypes
        {
            get
            {
                return new string[0];
            }
        }

        protected override void LoadDialogExtention()
        {
            StartNextAction();
        }

        private void TriggerBulkUpdate(bool selectedOnly)
        {
            ApplicationController.DoOnAsyncThread(() =>
            {
                var recordsToUpdate = GetRecordsToProcess(selectedOnly);

                var request = new BulkUpdateRequest(new RecordType(QueryViewModel.RecordType, RecordService.GetDisplayName(QueryViewModel.RecordType)), recordsToUpdate);
                var bulkUpdateDialog = new BulkUpdateDialog(RecordService, (IDialogController)ApplicationController.ResolveType(typeof(IDialogController)), request, () => { ClearChildForms(); QueryViewModel.DynamicGridViewModel.ReloadGrid(); });
                LoadChildForm(bulkUpdateDialog);
            });
        }

        private void TriggerBulkReplace(bool selectedOnly)
        {
            ApplicationController.DoOnAsyncThread(() =>
            {
                var recordsToUpdate = GetRecordsToProcess(selectedOnly);

                var request = new BulkReplaceRequest(new RecordType(QueryViewModel.RecordType, RecordService.GetDisplayName(QueryViewModel.RecordType)), recordsToUpdate);
                var bulkReplaceDialog = new BulkReplaceDialog(RecordService, (IDialogController)ApplicationController.ResolveType(typeof(IDialogController)), request, () => { ClearChildForms(); QueryViewModel.DynamicGridViewModel.ReloadGrid(); });
                LoadChildForm(bulkReplaceDialog);
            });
        }

        private IEnumerable<IRecord> GetRecordsToProcess(bool selectedOnly)
        {
            IEnumerable<IRecord> recordsToUpdate = null;

            var fieldsToGet = new List<string>();
            fieldsToGet.Add(RecordService.GetPrimaryKey(QueryViewModel.RecordType));
            var primaryField = RecordService.GetPrimaryField(QueryViewModel.RecordType);
            if (!string.IsNullOrWhiteSpace(primaryField))
                fieldsToGet.Add(primaryField);

            if (selectedOnly)
            {
                var ids = QueryViewModel.DynamicGridViewModel.SelectedRows.Select(r => r.Record.Id).ToArray();
                recordsToUpdate = RecordService.RetrieveAllOrClauses(QueryViewModel.RecordType, ids.Select(i => new Condition(RecordService.GetPrimaryKey(QueryViewModel.RecordType), ConditionType.Equal, i)), fieldsToGet);
            }
            else
            {
                var query = QueryViewModel.GenerateQuery();
                query.Fields = fieldsToGet;
                query.Top = -1;
                recordsToUpdate = RecordService.RetreiveAll(query);
            }

            return recordsToUpdate;
        }

        private void TriggerBulkDelete(bool selectedOnly)
        {
            ApplicationController.DoOnAsyncThread(() =>
            {
                var recordsToUpdate = GetRecordsToProcess(selectedOnly);

                var request = new BulkDeleteRequest(new RecordType(QueryViewModel.RecordType, RecordService.GetDisplayName(QueryViewModel.RecordType)), recordsToUpdate);
                var bulkUpdateDialog = new BulkDeleteDialog(RecordService, (IDialogController)ApplicationController.ResolveType(typeof(IDialogController)), request, () => { ClearChildForms(); QueryViewModel.DynamicGridViewModel.ReloadGrid(); });
                LoadChildForm(bulkUpdateDialog);
            });
        }

        private void TriggerBulkCopyFieldValue(bool selectedOnly)
        {
            ApplicationController.DoOnAsyncThread(() =>
            {
                var recordsToUpdate = GetRecordsToProcess(selectedOnly);

                var request = new BulkCopyFieldValueRequest(new RecordType(QueryViewModel.RecordType, RecordService.GetDisplayName(QueryViewModel.RecordType)), recordsToUpdate);
                var bulkUpdateDialog = new BulkCopyFieldValueDialog(RecordService, (IDialogController)ApplicationController.ResolveType(typeof(IDialogController)), request, () => { ClearChildForms(); QueryViewModel.DynamicGridViewModel.ReloadGrid(); });
                LoadChildForm(bulkUpdateDialog);
            });
        }
    }
}
