﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Linq.Expressions;
using JosephM.Prism.XrmModule.Test;
using JosephM.Record.Application.Fakes;
using JosephM.Record.Application.RecordEntry;
using JosephM.Record.Application.RecordEntry.Field;
using JosephM.Record.Application.RecordEntry.Form;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Microsoft.Xrm.Sdk;
using JosephM.Core.FieldType;
using JosephM.Core.Utility;
using JosephM.Record.Application.SettingTypes;
using JosephM.Record.Xrm.Test;
using JosephM.Record.Xrm.XrmRecord;
using JosephM.Xrm.ImportExporter.Service;

namespace JosephM.Xrm.ImporterExporter.Test
{
    [TestClass]
    public class XrmImporterExporterRequestTest : XrmModuleTest
    {
        [TestMethod]
        public void Debug()
        {
            var types = new[]
            {
                "adx_accesscontrolrule_publishingstate",
                "adx_accountaccess",
                "adx_autonumberingdefinition",
                "adx_brand",
                "adx_caseaccess",
                "adx_contactaccess",
                "adx_contentsnippet",
                "adx_entityform",
                "adx_entityformmetadata",
                "adx_entitylist",
                "adx_entitypermission",
                "adx_forum_activityalert",
                "adx_forumthreadtype",
                "adx_pagealert",
                "adx_pagecomment",
                "adx_pagecommentrating",
                "adx_pagenotification",
                "adx_pagetag",
                "adx_pagetag_webpage",
                "adx_pagetemplate",
                "adx_publishingstate",
                "adx_publishingstatetransitionrule",
                "adx_rating",
                "adx_redirect",
                "adx_review",
                "adx_setting",
                "adx_shortcut",
                "adx_sitemarker",
                "adx_sitesetting",
                "adx_store",
                "adx_tag",
                "adx_webfile",
                "adx_webform",
                "adx_webformmetadata",
                "adx_webformsession",
                "adx_webformstep",
                "adx_weblink",
                "adx_weblinkset",
                "adx_webnotificationurl",
                "adx_webpage",
                "adx_webpage_tag",
                "adx_webpageaccesscontrolrule",
                "adx_webrole",
                "adx_website",
                "adx_websiteaccess",
                "adx_websitebinding",
                "adx_webtemplate",
                "myr_autonumber",
                "myr_bankresponsecodemapping",
                "myr_charitablefundtype",
                "myr_contacttypeoption",
                "myr_donationtype",
                "myr_financialyear",
                "myr_impactarea",
                "myr_lmcfsettings",
                "myr_mypaymentresponsemapping",
                "myr_mypaymentsettings",
                "myr_organisationtypeoption",
                "myr_scheduledworkflow",
                "myr_securepaystatuscodemapping",
                "myr_taxgroup",
                "pricelevel"
            };

            var req = new XrmImporterExporterRequest();
            req.ImportExportTask = ImportExportTask.ExportXml;
            req.RecordTypes = types.Select(t => new ImportExportRecordType() {RecordType = new RecordType(t, t)});
            var controller = new FakeApplicationController();
            controller.SeralializeObjectToFile(req, @"C:\Users\josephm\Documents\objects.xml");
        }

        [TestMethod]
        public void XrmImporterExporterRequestCheckSerialise()
        {
            //to do refactor this generic and could use to validate all types

            var me = new XrmImporterExporterRequest();
            var fileName = Path.Combine(TestingFolder, "testobject.xml");
            if(File.Exists(fileName))
                File.Delete(fileName);
            Assert.IsFalse(File.Exists(fileName));
            PopulateObject(me);
            var controller = new FakeApplicationController();
            controller.SeralializeObjectToFile(me, fileName);

            Assert.IsTrue(File.Exists(fileName));
        }

        /// <summary>
        /// Runs through loading object into view model, adding rows and editing child forms
        /// </summary>
        [TestMethod]
        public void XrmImporterExporterRequestForXmlExport()
        {
            PrepareTests();

            //ensure a record for lookup
            Assert.IsNotNull(TestAccount);

            //Create the objetc and load into view model
            var req = new XrmImporterExporterRequest();
            req.FolderPath = new Folder(TestingFolder);
            req.ImportExportTask = ImportExportTask.ExportXml;

            var exportType = new ImportExportRecordType();
            exportType.RecordType = new RecordType("activitypointer", "Activity");
            req.RecordTypes = new[] {exportType};

            //get record types grid and check get edit row works
            var mainViewModel = CreateObjectEntryViewModel(req);
            var recordTypeGrid = mainViewModel.SubGrids.First(r => r.ReferenceName == "RecordTypes");
            var recordType = recordTypeGrid.GridRecords.First();
            var recordTypeEditViewModel = recordTypeGrid.GetEditRowViewModel(recordType);

            //get exclude fiields grid and check add row works
            var excludeFieldsGrid = recordTypeEditViewModel.SubGrids.First(sg => sg.ReferenceName == "ExcludeFields");
            excludeFieldsGrid.AddRow();
            var row = excludeFieldsGrid.GridRecords.First();
            Assert.IsTrue(row.GetRecordFieldFieldViewModel("RecordField").ItemsSource.Any());

            //this bit validates creating child forms
            //including the dependant lookup types etc. cascade to child forms
            var recordTypeViewModel = recordTypeEditViewModel.GetRecordTypeFieldViewModel("RecordType");
            recordTypeViewModel.Value = new RecordType("account", "Account");
            var exportTypeViewModel = recordTypeEditViewModel.GetPicklistFieldFieldViewModel("Type");
            exportTypeViewModel.ValueObject = ExportType.SpecificRecords;
            Assert.IsFalse(excludeFieldsGrid.GridRecords.Any());

            excludeFieldsGrid.AddRow();
            excludeFieldsGrid.AddRow();
            var row1 = excludeFieldsGrid.GridRecords.First();
            var field1 = row1.GetRecordFieldFieldViewModel("RecordField");
            var option1 = field1.ItemsSource.First();
            field1.Value = new RecordField(option1.Key, option1.Value);
            var row2 = excludeFieldsGrid.GridRecords.ElementAt(1);
            var field2 = row2.GetRecordFieldFieldViewModel("RecordField");
            var option2 = field2.ItemsSource.ElementAt(1);
            field2.Value = new RecordField(option2.Key, option2.Value);

            var specificRecordsViewModel = recordTypeEditViewModel.GetSubGridViewModel("OnlyExportSpecificRecords");
            specificRecordsViewModel.AddRow();
            var specifcRow = specificRecordsViewModel.GridRecords.First();
            var specificRowRecordLookup = specifcRow.GetLookupFieldFieldViewModel("Record");
            //so above we set the forms type to account so the record lookup in the grid should have target type of account (due to lookupfor attribute)
            Assert.AreEqual("account", specificRowRecordLookup.RecordTypeToLookup);
            //when open an edit row it should retain that record type
            var editSpecifcRow = specificRecordsViewModel.GetEditRowViewModel(specifcRow);
            var sections = editSpecifcRow.FormSectionsAsync;
            var editSpecificRowRecordLookup = editSpecifcRow.GetLookupFieldFieldViewModel("Record");
            Assert.AreEqual("account", editSpecificRowRecordLookup.RecordTypeToLookup);
            editSpecificRowRecordLookup.EnteredText = TestAccount.GetStringField("name");
            editSpecificRowRecordLookup.Search();
            Assert.IsTrue(editSpecificRowRecordLookup.LookupGridViewModel.GridRecords.Any());
            editSpecificRowRecordLookup.OnRecordSelected(editSpecificRowRecordLookup.LookupGridViewModel.GridRecords.First().Record);
            editSpecifcRow.OnSave();
            specifcRow = specificRecordsViewModel.GridRecords.First();
            specificRowRecordLookup = specifcRow.GetLookupFieldFieldViewModel("Record");
            Assert.IsNotNull(specificRowRecordLookup.EnteredText);

            Assert.IsTrue(recordTypeEditViewModel.Validate());
            recordTypeEditViewModel.OnSave();

            recordTypeGrid = mainViewModel.SubGrids.First(r => r.ReferenceName == "RecordTypes");
            recordType = recordTypeGrid.GridRecords.First();
        }
    }
}