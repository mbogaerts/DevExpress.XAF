﻿using System;
using Fasterflect;
using Xpand.Extensions.XAF.AppDomainExtensions;
using static System.AppDomain;
using static DevExpress.Persistent.Base.Tracing;

namespace Xpand.Extensions.XAF.XafApplicationExtensions{
    public static partial class XafApplicationExtensions{
        public static void HandleException(this DevExpress.ExpressApp.XafApplication application, Exception exception){
            Tracer.LogError(exception);
            try {
                var platform = application.GetPlatform();
                switch (platform) {
                    case Platform.Win:
                        application.CallMethod("HandleException", exception);
                        break;
                    case Platform.Web:
                        CurrentDomain.XAF().ErrorHandling().CallMethod("SetPageError", exception);
                        break;
                    default:
                        Console.WriteLine(exception);
                        break;
                }
            }
            catch (Exception e){
                Tracer.LogError(e);
                throw;
            }
        }
    }
}