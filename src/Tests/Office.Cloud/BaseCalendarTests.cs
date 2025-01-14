﻿using System.Threading.Tasks;
using JetBrains.Annotations;

namespace Xpand.XAF.Modules.Office.Cloud.Tests{
    public abstract class BaseCalendarTests:BaseCloudTests{
        
        [UsedImplicitly]
        public abstract Task Map_Two_New_Events();
        [UsedImplicitly]
        public abstract Task Map_Existing_Event_Two_Times();
        [UsedImplicitly]
        public abstract Task Delete_Two_Events();
        [UsedImplicitly]
        public abstract Task Delete_Local_Event_Resource();
        public abstract Task Delete_Cloud_Event();
        public abstract Task Insert_Cloud_Event();
        public abstract Task Update_Cloud_Event();

        [UsedImplicitly]
        public abstract Task Customize_Two_New_Event();

        [UsedImplicitly]
        public abstract Task Customize_Map_Existing_Event_Two_Times();

        [UsedImplicitly]
        public abstract Task Customize_Delete_Two_Events(bool handleDeletion);

        
    }
}