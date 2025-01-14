﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Reactive.Linq;
using System.Threading.Tasks;
using DevExpress.ExpressApp;
using DevExpress.Persistent.Base.General;
using DevExpress.Persistent.BaseImpl;
using Microsoft.Graph;
using Microsoft.Identity.Client;
using Shouldly;
using Xpand.Extensions.Office.Cloud;
using Xpand.Extensions.Reactive.Transform;
using Xpand.Extensions.Reactive.Utility;
using Xpand.Extensions.XAF.XafApplicationExtensions;
using Xpand.TestsLib;
using Xpand.XAF.Modules.Office.Cloud.Microsoft.Tests;
using Xpand.XAF.Modules.Reactive;
using Event = Microsoft.Graph.Event;
using Platform = Xpand.Extensions.XAF.XafApplicationExtensions.Platform;
using Task = System.Threading.Tasks.Task;

namespace Xpand.XAF.Modules.Office.Cloud.Microsoft.Calendar.Tests{
	static class CalendarTestExtensions{
        public const string CalendarName = "Xpnad Events";
        public const string PagingCalendarName = "Xpand Paging Events";

        public const int PagingCalendarItemsCount = 11;

        public static async Task<GraphServiceClient> MSGraphClient(this XafApplication application,bool deleteAll=false){
            application.ObjectSpaceProvider.NewAuthentication();
            var authorizeMS = await application.AuthorizeMS((exception, client) => Observable.Empty<AuthenticationResult>());
            if (deleteAll){
                await authorizeMS.Me.Calendar.DeleteAllEvents();
            }
            return authorizeMS;
        }


        public static async Task<(ICalendarRequestBuilder requestBuilder, Frame frame, GraphServiceClient client)>
            InitializeService(this XafApplication application, string calendarName = CalendarName, bool keepTasks = false, bool keepEvents = false,bool newAuthentication=true){
            var modelTodo = application.Model.ToReactiveModule<IModelReactiveModuleOffice>().Office.Microsoft().Calendar();
            modelTodo.DefaultCalendarName = calendarName;
            var client =  await application.InitGraphServiceClient(newAuthentication);
            var requestBuilder = client.client.Me.Calendars;
            var calendar = await requestBuilder.GetCalendar(calendarName, !keepEvents && calendarName!=PagingCalendarName).ToTaskWithoutConfigureAwait();
            if (calendarName==PagingCalendarName){
                calendar ??= await requestBuilder.Request().AddAsync(new global::Microsoft.Graph.Calendar(){Name = PagingCalendarName});
                var count = (await requestBuilder[calendar?.Id].Events.ListAllItems().Sum(entities => entities.Length));
                var itemsCount = PagingCalendarItemsCount-count;
                if (itemsCount>0){
                    await requestBuilder[calendar?.Id].NewCalendarEvents(itemsCount, nameof(MicrosoftCalendarModule));
                }
            }
            
            if (calendarName != PagingCalendarName&&!keepTasks&&!keepEvents){
                await requestBuilder[calendar?.Id].DeleteAllEvents();
                (await requestBuilder[calendar?.Id].Events.ListAllItems()).Length.ShouldBe(0);
            }
            
            return (requestBuilder[calendar?.Id],client.frame,client.client);
        }

        public static async Task<(Frame frame, GraphServiceClient client)> InitGraphServiceClient(this XafApplication application,bool newAuthentication=true){
	        if (newAuthentication){
		        application.ObjectSpaceProvider.NewAuthentication();
	        }
            
            var todoModel = application.Model.ToReactiveModule<IModelReactiveModuleOffice>().Office.Microsoft().Calendar();
            var window = application.CreateViewWindow();
            var service = CalendarService.Client.Select(tuple => tuple).FirstAsync().SubscribeReplay();
            var modelObjectView = todoModel.Items.Select(item => item.ObjectView).First();
            window.SetView(application.NewView(modelObjectView));
            return (await service.ToTaskWithoutConfigureAwait());
        }

        public static MicrosoftCalendarModule CalendarModule(this Platform platform,params ModuleBase[] modules){
            var application = NewApplication(platform,  modules);
            application.SetupSecurity(true);
            var module = application.AddModule<MicrosoftCalendarModule>(typeof(DevExpress.Persistent.BaseImpl.Event));
            application.Model.ConfigureMicrosoft();
            var todoModel = application.Model.ToReactiveModule<IModelReactiveModuleOffice>().Office.Microsoft().Calendar();
            var dependency = ( todoModel).Items.AddNode<IModelCalendarItem>();
            dependency.ObjectView = application.Model.BOModel.GetClass(typeof(DevExpress.Persistent.BaseImpl.Event)).DefaultDetailView;
            application.Logon();
            application.CreateObjectSpace();
            return module.Application.Modules.OfType<MicrosoftCalendarModule>().First();  
        }

        static XafApplication NewApplication(this Platform platform,  ModuleBase[] modules){
            var xafApplication = platform.NewApplication<MicrosoftCalendarModule>();
            xafApplication.Modules.AddRange(modules);
            return xafApplication;
        }

        public static void AssertEvent(this IObjectSpaceProvider objectSpaceProvider, Type cloudEntityType, DevExpress.Persistent.BaseImpl.Event @event,
            string title, DateTime? due, string taskId, string localEventSubject){
            title.ShouldBe(localEventSubject);
            
            due.ShouldNotBeNull();


            using var space = objectSpaceProvider.CreateObjectSpace();
            var cloudObjects = space.QueryCloudOfficeObject(cloudEntityType,@event).ToArray();
            cloudObjects.Length.ShouldBe(1);
            var cloudObject = cloudObjects.First();
            cloudObject.LocalId.ShouldBe(@event.Oid.ToString());
            cloudObject.CloudId.ShouldBe(taskId);
        }
        public static async Task Delete_Event_Resource<TEvent>(this IObjectSpace objectSpace, Func<DevExpress.Persistent.BaseImpl.Event, IObservable<TEvent>> synchronizeEvents,
            Func<Task> assert,TimeSpan timeout){
            
            var localEvent = objectSpace.NewEvent();
            var calendarEvents = synchronizeEvents(localEvent)
                .FirstAsync().SubscribeReplay();
            objectSpace.CommitChanges();
            await calendarEvents.Timeout(timeout);

            calendarEvents = synchronizeEvents(localEvent)
                .FirstAsync().SubscribeReplay();
            localEvent.Resources.Remove(localEvent.Resources.First());
            objectSpace.CommitChanges();


            await calendarEvents.Timeout(timeout);

            await assert();
        }

        public static async Task<IList<(DevExpress.Persistent.BaseImpl.Event local, Event cloud)>> CreateExistingObjects(
            this XafApplication application, string title,int count=1){
            var builder =await application.AuthorizeTestMS();
            var calendar = await builder.Me.Calendars.GetCalendar(CalendarName, true);
            await builder.Me.Calendars[calendar.Id].DeleteAllEvents();
            return await Observable.Range(0, count)
                .SelectMany(i => builder.Me.Calendars[calendar.Id].NewCalendarEvents(1, title)
                    .SelectMany(lst => lst).Select(outlookTask1 => (application.NewEvent(), outlookTask1)))
                .Buffer(count);
        }

        public static void Modify_Event<TTask>(this TTask task,  int i) where TTask:IEvent{
            task.Subject = $"{nameof(Modify_Event)}{i}";
        }
        
        public static DevExpress.Persistent.BaseImpl.Event NewEvent(this XafApplication application){
	        using var objectSpace = application.CreateObjectSpace();
	        var newEvent = objectSpace.NewEvent();
	        objectSpace.CommitChanges();
	        return newEvent;
        }
        public static DevExpress.Persistent.BaseImpl.Event NewEvent(this IObjectSpace objectSpace,int index=0) {
            var @event = objectSpace.CreateObject<DevExpress.Persistent.BaseImpl.Event>();
            @event.Subject = $"Subject{index}";
            
            @event.StartOn=DateTime.Now.AddDays(1);
            @event.EndOn = @event.StartOn.AddMinutes(30);
            var resource = objectSpace.CreateObject<Resource>();
            resource.Caption = "organizer@mail.com";
            @event.Resources.Add(resource);
            return @event;
        }

        public static IObservable<IList<Event>> NewCalendarEvents(this ICalendarRequestBuilder builder,int count,string title){
            var dateTime = DateTime.Now;
            return Observable.Range(0, count).SelectMany(i => {
                var task = new Event(){
                    Subject = $"{i}{title}",
                    End = DateTimeTimeZone.FromDateTime(dateTime),Start = DateTimeTimeZone.FromDateTime(dateTime)
                };
                
                return builder.Events.Request().AddAsync(task);
            }).Buffer(count);
        }
        
    }
}