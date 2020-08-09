﻿using System;
using System.Linq;
using System.Reactive.Linq;
using System.Threading.Tasks;
using akarnokd.reactive_extensions;
using DevExpress.ExpressApp;
using Microsoft.Identity.Client;
using NUnit.Framework;
using Xpand.TestsLib;
using Xpand.TestsLib.Attributes;
using Xpand.XAF.Modules.Office.Cloud.Microsoft.BusinessObjects;
using Xpand.XAF.Modules.Office.Cloud.Tests;
using Platform = Xpand.Extensions.XAF.XafApplicationExtensions.Platform;

namespace Xpand.XAF.Modules.Office.Cloud.Microsoft.Tests{
	public class MicrosoftServiceTests:CloudServiceTests<MSAuthentication>{
        [Test][XpandTest()]
        public async Task Actions_Active_State_when_authentication_not_needed([ValueSource(nameof(PlatformDatasource))]Platform platform){
            using (var application=Application(platform)){
                NewAuthentication(platform, application);
                await application.Actions_Active_State_when_authentication_not_needed(ServiceName);
            }
        }

        protected MicrosoftModule MicrosoftModule( Platform platform=Platform.Win,params ModuleBase[] modules){
            var application = NewApplication(platform,  modules);
            application.SetupSecurity();
            var module = application.AddModule<MicrosoftModule>();
            application.Model.ConfigureMicrosoft();
            application.Logon();
            application.CreateObjectSpace();
            return module.Application.Modules.OfType<MicrosoftModule>().First();
        }
        XafApplication NewApplication(Platform platform,  ModuleBase[] modules){
            var xafApplication = platform.NewApplication<MicrosoftModule>();
            xafApplication.Modules.AddRange(modules);
            return xafApplication;
        }

        protected override IObservable<bool> NeedsAuthentication(XafApplication application) => application.MicrosoftNeedsAuthentication();

        protected override XafApplication Application(Platform platform) => MicrosoftModule(platform).Application;

        protected override void NewAuthentication(Platform platform, XafApplication application) => application.ObjectSpaceProvider.NewAuthentication(platform);

        protected override string ServiceName => "Microsoft";

        protected override void OnConnectMicrosoft_Action_Creates_Connection(Platform platform, XafApplication application) 
            => MicrosoftService.CustomAquireTokenInteractively
                .Do(args => application.ObjectSpaceProvider.NewAuthentication(platform))
                .Do(e => e.Instance=Observable.Empty<AuthenticationResult>().FirstOrDefaultAsync()).Test();
    }
}