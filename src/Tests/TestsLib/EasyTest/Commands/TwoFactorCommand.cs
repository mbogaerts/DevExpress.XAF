﻿using System;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Windows.Forms;
using DevExpress.EasyTest.Framework;
using NUnit.Framework;
using Xpand.Extensions.AppDomainExtensions;
using Xpand.TestsLib.EasyTest.Commands.Automation;

namespace Xpand.TestsLib.EasyTest.Commands{
    public class TwoFactorCommand : EasyTestCommand{
        private string _winAuthPath;
        private readonly string _authenticatorSettingsPath;

        public TwoFactorCommand(string winAuthPath,string authenticatorSettingsPath){
            _winAuthPath = winAuthPath;
            _authenticatorSettingsPath = authenticatorSettingsPath;
        }

        protected override void ExecuteCore(ICommandAdapter adapter){
            Process.GetProcessesByName("WinAuth").FirstOrDefault()?.Kill();
            var winAuthSettingsDir = $"{Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData)}\\WinAuth";
            if (!Directory.Exists(winAuthSettingsDir)){
                Directory.CreateDirectory(winAuthSettingsDir);
            }
            var winAuthSettings = $"{winAuthSettingsDir}\\winauth.xml";
            var winAuthSettingsBackup = $"{winAuthSettingsDir}\\winauth{DateTime.Now.Ticks}.xml";
            try{
                if (File.Exists(winAuthSettings)){
                    File.Move(winAuthSettings, winAuthSettingsBackup);
                }
                // TestContext.Out.WriteLine($"bbbbbbbbbbbbbbbb{AppDomain.CurrentDomain.ApplicationPath()}\\..\\WinAuth.exe");
                File.Copy(_authenticatorSettingsPath, winAuthSettings);
                _winAuthPath = @"D:\a\1\s\bin\Tests\WinAuth.exe";
                if (!File.Exists(_winAuthPath)){
                    TestContext.Out.WriteLine("NOT FOUND");
                }
                Process.Start(_winAuthPath);
                adapter.Execute(new WaitWindowFocusCommand("WinAuth"));
                adapter.Execute(new MoveWindowCommand(0, 0, 420, 180));
                adapter.Execute(new MouseCommand(new Point(120, 100), simulator => simulator.RightButtonClick()));
                adapter.Execute(new WaitCommand(1000));
                adapter.Execute(new MouseCommand(new Point(160, 185)));
            }
            finally{
                Process.GetProcessesByName("WinAuth").FirstOrDefault()?.Kill();
                if (File.Exists(winAuthSettingsBackup)){
                    if (File.Exists(winAuthSettings)){
                        File.Delete(winAuthSettings);
                    }
                    File.Move(winAuthSettingsBackup,winAuthSettings);
                }
                adapter.Execute(new WaitCommand(1000));
                var text = Clipboard.GetText();
                if (!string.IsNullOrEmpty(text)){
                    adapter.Execute(new SendTextCommand(text));
                }
            }
            
        }
    }
}