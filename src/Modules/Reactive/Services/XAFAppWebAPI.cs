﻿using System;
using System.Linq;
using System.Reactive;
using System.Reactive.Concurrency;
using System.Reactive.Linq;
using System.Threading;
using DevExpress.ExpressApp;
using Fasterflect;
using JetBrains.Annotations;
using Xpand.Extensions.AppDomainExtensions;
using Xpand.Extensions.Reactive.Filter;
using Xpand.Extensions.Reactive.Transform;
using Xpand.Extensions.XAF.AppDomainExtensions;
using Xpand.Extensions.XAF.XafApplicationExtensions;

namespace Xpand.XAF.Modules.Reactive.Services{
    public interface IXAFAppWebAPI{
        XafApplication Application{ get; }
    }

    class XAFAppWebAPI:IXAFAppWebAPI{
        public XafApplication Application{ get; }

        public XAFAppWebAPI(XafApplication application){
            Application = application;
        }

    }

    public static class XafWebApiExtensions{
        public static IObservable<(string parameter, object result)> WhenCallBack(this IObservable<IXAFAppWebAPI> source,string parameter=null) 
            => source.SelectMany(api => api.Application.WhenWindowCreated().When(TemplateContext.ApplicationWindow)
                    .TemplateChanged()
                    .SelectMany(_ => Observable.FromEventPattern<EventArgs>(AppDomain.CurrentDomain.XAF().CurrentRequestPage(),"InitComplete",ImmediateScheduler.Instance).To(_))
                    .Select(_ => _.Template.GetPropertyValue("CallbackManager").GetPropertyValue("CallbackControl"))
                    .SelectMany(_ => Observable.FromEventPattern<EventArgs>(_,"Callback",ImmediateScheduler.Instance)))
                .Select(_ => (parameter:$"{_.EventArgs.GetPropertyValue("Parameter")}",result:_.EventArgs.GetPropertyValue("Result")))
                .Where(_ => parameter==null||_.parameter.StartsWith($"{parameter}:"));

        public static IObservable<Unit> CheckAsync(this IObservable<IXAFAppWebAPI> source,string module) 
            => source.Where(api => api.Application.GetPlatform()!=Platform.Blazor)
                .SelectMany(api => {
                    var whenTemplate = source.SelectMany(webAPI => webAPI.Application.WhenWindowCreated().When(TemplateContext.ApplicationWindow).FirstAsync().TemplateChanged())
                        .WhenNotDefault(window => window.Template).Publish().RefCount();
                    return whenTemplate
                        .WhenDefault(window => (bool)window.Template.GetPropertyValue("IsAsync")).ToUnit()
                        .Do(window => AppDomain.CurrentDomain.Web().WriteHttpResponse($"<span style='color:red'>Asynchronous operations not supported, please mark the Page as async, for details refer to {module} wiki page. </span>",true))
                        .Merge(whenTemplate.SelectMany(_ => SynchronizationContext.Current.ReturnObservable().Where(context => context.GetType().Name!="AspNetSynchronizationContext")
                            .Do(context => AppDomain.CurrentDomain.Web().WriteHttpResponse($"<span style='color:red'>{context.GetType().FullName} is used instead of System.Web.AspNetSynchronizationContext, please modify your httpRuntime configuration. For details refer to {module} wiki page.</span>",true)).ToUnit()));
                });

        public static IObservable<IXAFAppWebAPI> WhenWeb(this XafApplication application) 
            => application.GetPlatform() == Platform.Win ?  Observable.Empty<IXAFAppWebAPI>():new XAFAppWebAPI(application).ReturnObservable();

        [PublicAPI]
        public static void SetPageError(this IXAFAppWebAPI api, Exception exception) 
            => api.Application.HandleException(exception);

        [PublicAPI]
        public static void Redirect(this IXAFAppWebAPI api, string url,bool endResponse=true){
            if (api.Application.GetPlatform() == Platform.Blazor){
                api.GetService("Microsoft.AspNetCore.Components.NavigationManager").CallMethod("NavigateTo", url,endResponse);
            }
            else{
                AppDomain.CurrentDomain.XAF().WebApplicationType()
                    .GetMethod("Redirect", new[]{typeof(string), typeof(bool)})
                    ?.Invoke(null, new object[]{url, endResponse});
            }
        }

        public static T GetService<T>(this IXAFAppWebAPI api) => (T) api.GetService(typeof(T));

        public static object GetService(this IXAFAppWebAPI api,Type serviceType) 
            => api.Application.GetPropertyValue("ServiceProvider").CallMethod("GetService", serviceType);
        
        public static object GetService(this IXAFAppWebAPI api,string serviceType) 
            => AppDomain.CurrentDomain.GetAssemblyType(serviceType).Select(api.GetService).FirstOrDefault();

        public static object HttpContext(this IXAFAppWebAPI api)
            => api.Application.GetPlatform() != Platform.Blazor ? AppDomain.CurrentDomain.Web().HttpContext()
                : api.GetService("Microsoft.AspNetCore.Http.IHttpContextAccessor").GetPropertyValue("HttpContext");

        public static Uri GetRequestUri(this IXAFAppWebAPI api) 
            => (Uri) (api.Application.GetPlatform() == Platform.Blazor
                ? new Uri(AppDomain.CurrentDomain.GetAssemblyType("Microsoft.AspNetCore.Http.Extensions.UriHelper").First().Method("GetDisplayUrl", Flags.StaticPublic).Call(null, api.HttpContext().GetPropertyValue("Request")).ToString())
                : api.HttpContext().GetPropertyValue("Request").GetPropertyValue("Url"));
    }
}