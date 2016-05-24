﻿using JosephM.Application.ViewModel.Dialog;
using JosephM.Application.ViewModel.Shared;
using JosephM.Core.Log;
using JosephM.Core.Service;

namespace JosephM.XRM.VSIX.Dialogs
{
    public class VsixServiceDialog<TService, TRequest, TResponse, TResponseItem>
        : DialogViewModel
        where TService : ServiceBase<TRequest, TResponse, TResponseItem>
        where TRequest : ServiceRequestBase
        where TResponse : ServiceResponseBase<TResponseItem>, new()
        where TResponseItem : ServiceResponseItem
    {
        public TService Service { get; set; }
        public TRequest Request { get; set; }

        public VsixServiceDialog(TService service, TRequest request, DialogController dialogController)
            : base(dialogController)
        {
            Service = service;
            Request = request;
        }

        protected override void LoadDialogExtention()
        {
            StartNextAction();
        }

        protected override void CompleteDialogExtention()
        {
            LoadingViewModel.IsLoading = false;
            IsProcessing = true;

            var progressControlViewModel = new ProgressControlViewModel(ApplicationController);
            Controller.LoadToUi(progressControlViewModel);
            var progressControlViewModelLevel2 = new ProgressControlViewModel(ApplicationController);
            Controller.LoadToUi(progressControlViewModelLevel2);
            var controller = new LogController(progressControlViewModel);
            controller.AddLevel2Ui(progressControlViewModelLevel2);

            var response = Service.Execute(Request, controller);

            Controller.RemoveFromUi(progressControlViewModel);
            Controller.RemoveFromUi(progressControlViewModelLevel2);

            foreach (var responseItem in response.ResponseItems)
            {
                CompletionItems.Add(responseItem);
            }
            //if (Request.GetType().GetCustomAttributes(typeof(AllowSaveAndLoad), false).Any())
            //{
            //    AddCompletionOption("Save Request", SaveRequest);
            //}

            //if (Response.Success)
            //    ProcessCompletionExtention();

            IsProcessing = false;

            if (!response.Success)
                ProcessError(response.Exception);
            else if (string.IsNullOrWhiteSpace(CompletionMessage))
                CompletionMessage = "Process Finished";


        }
    }
}