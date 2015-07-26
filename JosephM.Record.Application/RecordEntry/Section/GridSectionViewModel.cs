﻿#region

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using JosephM.Core.Extentions;
using JosephM.Record.Application.Grid;
using JosephM.Record.Application.RecordEntry.Form;
using JosephM.Record.Application.RecordEntry.Metadata;
using JosephM.Record.Application.Shared;
using JosephM.Record.Application.Validation;
using JosephM.Record.IService;

#endregion

namespace JosephM.Record.Application.RecordEntry.Section
{
    public class GridSectionViewModel : SectionViewModelBase, IDynamicGridViewModel, IValidatable
    {
        private ObservableCollection<GridRowViewModel> _records;

        public GridSectionViewModel(SubGridSection subGridSection,
            RecordEntryFormViewModel recordForm)
            : base(subGridSection, recordForm)
        {
            AddRowButton = new XrmButtonViewModel("Add", AddRow, ApplicationController);
            DynamicGridViewModelItems = new DynamicGridViewModelItems()
            {
                CanDelete = true,
                CanEdit = true,
                DeleteRow = RemoveRow,
                EditRow = EditRow
            };
        }

        public XrmButtonViewModel AddRowButton { get; set; }

        public void AddRow()
        {
            try
            {
                var viewModel = FormService.GetLoadRowViewModel(SectionIdentifier, RecordForm, (record) =>
                {
                    InsertRecord(record, 0);
                    RecordForm.ClearChildForm();
                }, () => RecordForm.ClearChildForm());
                if (viewModel == null)
                    InsertRecord(RecordService.NewRecord(RecordType), 0);
                else
                {
                    RecordForm.LoadChildForm(viewModel);
                }
            }
            catch (Exception ex)
            {
                ApplicationController.UserMessage(string.Format("Error Adding Row: {0}", ex.DisplayString()));
            }
        }

        private void RemoveRow(GridRowViewModel row)
        {
            GridRecords.Remove(row);
        }

        private void EditRow(GridRowViewModel row)
        {
            try
            {
                var viewModel = GetEditRowViewModel(row);
                if (viewModel == null)
                {
                    throw new NotImplementedException("No Form For Type");
                }
                else
                    RecordForm.LoadChildForm(viewModel);
            }
            catch (Exception ex)
            {
                ApplicationController.UserMessage(string.Format("Error Adding Row: {0}", ex.DisplayString()));
            }
        }

        public RecordEntryFormViewModel GetEditRowViewModel(GridRowViewModel row)
        {
            var viewModel = FormService.GetEditRowViewModel(SectionIdentifier, RecordForm, (record) =>
            {
                RecordForm.ClearChildForm();
                var index = GridRecords.IndexOf(row);
                GridRecords.Remove(row);
                InsertRecord(record, index);
            }, () => RecordForm.ClearChildForm(), row);
            return viewModel;
        }

        private SubGridSection SubGridSection
        {
            get { return FormSection as SubGridSection; }
        }

        public string ReferenceName
        {
            get { return SubGridSection.LinkedRecordLookup; }
        }

        public ObservableCollection<GridRowViewModel> GridRecords
        {
            get
            {
                if (_records == null)
                {
                    LoadRowsAsync();
                }
                return _records;
            }
            set
            {
                _records = value;
                OnPropertyChanged("GridRecords");
            }
        }

        public IEnumerable<GridFieldMetadata> RecordFields
        {
            get { return SubGridSection.Fields; }
        }

        public GridRowViewModel SelectedRow { get; set; }

        public void DoWhileLoading(string message, Action action)
        {
            RecordForm.DoWhileLoading(message, action);
        }

        public bool IsReadOnly
        {
            get { return false; }
        }

        public void LoadRowsAsync()
        {
            var records = RecordService.GetLinkedRecords(
                SubGridSection.LinkedRecordType,
                RecordForm.RecordType,
                SubGridSection.LinkedRecordLookup,
                RecordForm.RecordId);
            DoOnMainThread(() => LoadRows(records));
        }

        private void LoadRows(IEnumerable<IRecord> records)
        {
            GridRecords = new ObservableCollection<GridRowViewModel>();
            foreach (var record in records)
            {
                AddRecord(record);
            }
        }

        private void InsertRecord(IRecord record, int index)
        {
            //DoOnMainThread(() =>
            //{
                var rowItem = new GridRowViewModel(record, this);
                GridRecords.Insert(index, rowItem);
            rowItem.OnLoad();
            //});
        }

        private void AddRecord(IRecord record)
        {
           // DoOnMainThread(() =>
            //{
                var rowItem = new GridRowViewModel(record, this);
                rowItem.OnLoad();
                GridRecords.Add(rowItem);
            //});
        }

        public override string RecordType
        {
            get { return SubGridSection.LinkedRecordType; }
        }


        public DynamicGridViewModelItems DynamicGridViewModelItems { get; set; }

        internal override bool Validate()
        {
            ErrorMessage = null;
            var isValid = true;
            if (IsVisible)
            {
                foreach (var gridRowViewModel in GridRecords)
                {
                    if (!gridRowViewModel.Validate())
                        isValid = false;
                }
                var thisValidators = FormService.GetSectionValidationRules(SectionIdentifier);

                foreach (var validator in thisValidators)
                {
                    var response = validator.Validate(this);
                    if (!response.IsValid)
                    {
                        ErrorMessage = response.ErrorContent != null ? response.ErrorContent.ToString() : "No Error Content";
                        //need to somehow get the error message into the grid
                        isValid = false;
                    }
                }
            }

            return isValid;
        }

        private bool _hasError;
        public bool HasError
        {
            get { return _hasError; }
            set
            {
                _hasError = value;
                OnPropertyChanged("HasError");
            }
        }

        private string _errorMessage;
        public string ErrorMessage
        {
            get { return _errorMessage; }
            set
            {
                _errorMessage = value;
                HasError = !_errorMessage.IsNullOrWhiteSpace();
                OnPropertyChanged("ErrorMessage");
            }
        }

        public override string SectionIdentifier
        {
            get { return ReferenceName; }
        }

        public RecordEntryViewModelBase GetRecordForm()
        {
            return RecordForm;
        }

        public void ClearRows()
        {
            ApplicationController.DoOnMainThread(() => GridRecords.Clear());
        }
    }
}