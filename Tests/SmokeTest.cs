using Tower.Core;
namespace Tower.WinForms;

internal static class SmokeTest
{
    [STAThread]
    public static int Run()
    {
        var output=Path.Combine(Environment.CurrentDirectory,"ui-smoke"); Directory.CreateDirectory(output);
        var forms=typeof(LegacyForm).Assembly.GetTypes().Where(t=>t.Name.StartsWith("Form_",StringComparison.Ordinal) && t.IsSubclassOf(typeof(LegacyForm))).ToList();
        if(forms.Count!=16) throw new InvalidOperationException("Expected all 16 VB6 forms.");
        foreach(var type in forms)
        {
            using var form=(LegacyForm)Activator.CreateInstance(type)!;
            form.Show(); Application.DoEvents();
            using var bitmap=new Bitmap(form.Width,form.Height); form.DrawToBitmap(bitmap,new Rectangle(Point.Empty,form.Size));
            bitmap.Save(Path.Combine(output,type.Name+".png")); form.Close();
        }
        // Also exercise the actual presenter and embedded artwork, not only empty designer forms.
        using var app=new TowerApplication(); app.Voice.Settings.ReadbacksEnabled=false; app.StartForm.Show(); Application.DoEvents();
        using(var bitmap=new Bitmap(app.StartForm.Width,app.StartForm.Height))
        { app.StartForm.DrawToBitmap(bitmap,new(Point.Empty,app.StartForm.Size)); bitmap.Save(Path.Combine(output,"Start-Wired.png")); }
        var simulation=new Simulation(new Session { Options=new() { AircraftPerMinute=0 } });
        using var main=new SimulationWindow(app,simulation); main.Show();
        simulation.AddAircraft(FlightDirection.Arrival,"TEST100"); simulation.AddAircraft(FlightDirection.Departure,"TEST101");
        Application.DoEvents();
        foreach(Form form in main.OwnedForms.Append(main))
        {
            using var bitmap=new Bitmap(form.Width,form.Height); form.DrawToBitmap(bitmap,new(Point.Empty,form.Size));
            bitmap.Save(Path.Combine(output,"Wired-"+form.Name+".png"));
        }
        main.Close();
        var canyonPath=Path.Combine(Environment.CurrentDirectory,"tests","Fixtures","Canyon");
        if(Directory.Exists(canyonPath))
        {
            var canyon=Tower.Infrastructure.OriginalData.LoadCanyon(canyonPath);
            var sim=new Simulation(new Session {Airport=canyon,Options=new(){Runway="9",AircraftPerMinute=0}});
            using var canyonWindow=new SimulationWindow(app,sim);canyonWindow.Show();
            sim.AddAircraft(FlightDirection.Arrival,"NWA135","B727");sim.AddAircraft(FlightDirection.Departure,"DAL253","B727");Application.DoEvents();
            foreach(var form in canyonWindow.OwnedForms.Append(canyonWindow))
            {using var bitmap=new Bitmap(form.Width,form.Height);form.DrawToBitmap(bitmap,new(Point.Empty,form.Size));bitmap.Save(Path.Combine(output,"Canyon-"+form.Name+".png"));}
            canyonWindow.Close();
        }
        // Exercise configured dialog presenters as well as parameterless designer constructors.
        void CaptureDialog(string name,Action open)
        {
            using var closer=new System.Windows.Forms.Timer { Interval=250 };
            closer.Tick+=(_,_)=>
            {
                closer.Stop();
                var dialog=Application.OpenForms.Cast<Form>().Last(f=>f!=app.StartForm);
                using var bitmap=new Bitmap(dialog.Width,dialog.Height);
                dialog.DrawToBitmap(bitmap,new(Point.Empty,dialog.Size)); bitmap.Save(Path.Combine(output,"Configured-"+name+".png"));
                dialog.DialogResult=DialogResult.Cancel; dialog.Close();
            };
            closer.Start(); open();
        }
        CaptureDialog("ASDESetup",()=>app.RadarSetup(false));
        CaptureDialog("BRITESetup",()=>app.RadarSetup(true));
        CaptureDialog("Lights",app.LightsSetup);
        CaptureDialog("Display",app.DisplaySetup);
        CaptureDialog("Weather",app.WeatherSetup);
        CaptureDialog("Commands",TowerApplication.ShowCommands);
        CaptureDialog("VoiceCatalog",app.BrowseVoices);
        foreach(var name in new[]{"menu_File_NewScenario","menu_File_SelectController","menu_File_ControllerStats","menu_Help_AboutTower"})
            CaptureDialog(name,()=>LegacyForm.FindItem(app.StartForm.MainMenuStrip!.Items,name)!.PerformClick());
        // Exercise Windows synthesis without depending on a physical audio device.
        using(var inventory=new System.Speech.Synthesis.SpeechSynthesizer())
        {
            if(inventory.GetInstalledVoices().Any(v=>v.Enabled && v.VoiceInfo.Culture.TwoLetterISOLanguageName=="en"))
            {
                using var synth=VoicePlayer.CreateEnglishSynthesizer();using var wave=new MemoryStream();
                synth.SetOutputToWaveStream(wave);synth.Speak("Test one, cleared to land runway one eight.");
                var data=wave.ToArray();if(data.Length<=44 || System.Text.Encoding.ASCII.GetString(data,0,4)!="RIFF") throw new InvalidOperationException("Windows speech did not produce a WAV.");
                File.WriteAllBytes(Path.Combine(output,"windows-speech-test.wav"),data);
                File.WriteAllText(Path.Combine(output,"speech-test.txt"),$"PASS English Windows speech to WAV: {synth.Voice.Name}, {data.Length} bytes. Physical playback not tested.");
            }
            else File.WriteAllText(Path.Combine(output,"speech-test.txt"),"SKIP Windows synthesis: no English desktop voice installed on runner.");
        }
        app.StartForm.Close();
        Console.WriteLine("16 layouts and wired simulation UI rendered successfully."); return 0;
    }
}
