using CoreAudio.Interfaces;
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Data;
using System.Diagnostics;
using System.Linq;
using System.Net;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;

namespace AudioBalancer
{
    public class MainWindowViewModel : INotifyPropertyChanged
    {
        private MainWindow _window;

        public string NSProcessPath => GetNirsoftProcessPath();

        public MainWindowViewModel(MainWindow window) {
            _window = window;
            _ = Task.Run(async () => await RefreshProcesses());
        }

        private string _processName = string.Empty;
        public string ProcessName
        {
            get { return _processName; }
            set { _processName = value; NotifyPropertyChanged("ProcessName"); }
        }
        private int _balance = 0;
        public int Balance
        {
            get { return _balance; }
            set { _balance = value; NotifyPropertyChanged("Balance"); }
        }


        private ObservableCollection<string> _processes = new ObservableCollection<string>();
        public IEnumerable<string> Processes
        {
            get 
            {
                var res = _processes.ToList();
                res.Sort();
                return res;
            }
            set 
            {
                _processes = new ObservableCollection<string>();
                foreach(var s in value)
                    _processes.Add(s);

                NotifyPropertyChanged("Processes");
            }
        }

        public async Task ProcessChanged()
        {
            Balance = await GetVolumeBalance();
            NotifyPropertyChanged("ProcessName");
            NotifyPropertyChanged("Balance");
        }

        public async Task<int> GetVolumeBalance()
        {
            var volL = await ExecuteNirsoftCommand("/GetVolumeChannel", ProcessName, "0");
            var volR = await ExecuteNirsoftCommand("/GetVolumeChannel", ProcessName, "1");

            if (volL > volR) return volL / volR * -100;
            if (volR > volL) return volR / volL * 100;
            return 0;
        }

        public async Task SetVolumeBalance()
        {
            var maxVol = await ExecuteNirsoftCommand("/GetPercent", ProcessName) / 10;
            var pc = 100 - maxVol * Math.Abs(Balance) / 100;

            if(Balance == 0)
                await ExecuteNirsoftCommand("/SetVolumeChannels", ProcessName, maxVol.ToString(), maxVol.ToString());
            else if(Balance > 0)
                await ExecuteNirsoftCommand("/SetVolumeChannels", ProcessName, pc.ToString(), maxVol.ToString());
            else
                await ExecuteNirsoftCommand("/SetVolumeChannels", ProcessName, maxVol.ToString(), pc.ToString());
        }

        public async Task<int> ExecuteNirsoftCommand(string command, params string[] args)
        {
            var p = new Process();
            p.StartInfo = new ProcessStartInfo(NSProcessPath, $"{command} {string.Join(" ", args)}");
            p.StartInfo.WindowStyle = ProcessWindowStyle.Hidden;
            p.StartInfo.UseShellExecute = false; 
            p.StartInfo.RedirectStandardOutput = true;
            p.StartInfo.UseShellExecute = false;
            p.StartInfo.CreateNoWindow = true;
            p.Start();
            await p.WaitForExitAsync();
            return p.ExitCode;
        }

        public string GetNirsoftProcessPath()
        {
            var path = System.AppDomain.CurrentDomain.BaseDirectory;
            var tpDir = Environment.Is64BitOperatingSystem ? "svcl-x64" : "svcl";
            return $"{path}ThirdParty/{tpDir}/svcl.exe";
        }

        public async Task RefreshProcesses()
        {
            Processes = GetAudioProcesses().Select(x => x.ProcessName);
        }

        public IEnumerable<Process> GetAudioProcesses()
        {
            IMMDeviceEnumerator deviceEnumerator = null;
            IAudioSessionEnumerator sessionEnumerator = null;
            IAudioSessionManager2 mgr = null;
            IMMDevice speakers = null;
            List<Process> audioProcesses = new List<Process>();
            try
            {
                // get the speakers (1st render + multimedia) device
                deviceEnumerator = (IMMDeviceEnumerator)(new MMDeviceEnumerator());
                deviceEnumerator.GetDefaultAudioEndpoint(EDataFlow.eRender, ERole.eMultimedia, out speakers);

                // activate the session manager. we need the enumerator
                Guid IID_IAudioSessionManager2 = typeof(IAudioSessionManager2).GUID;
                object o;
                speakers.Activate(ref IID_IAudioSessionManager2, 0, IntPtr.Zero, out o);
                mgr = (IAudioSessionManager2)o;

                // enumerate sessions for on this device
                mgr.GetSessionEnumerator(out sessionEnumerator);
                int count;
                sessionEnumerator.GetCount(out count);

                // search for an audio session with the required process-id
                for (int i = 0; i < count; ++i)
                {
                    IAudioSessionControl ctl1 = null;
                    IAudioSessionControl2 ctl = null;
                    try
                    {
                        sessionEnumerator.GetSession(i, out ctl1);
                        ctl = (IAudioSessionControl2)ctl1;
                        ctl.GetProcessId(out uint cpid);

                        audioProcesses.Add(Process.GetProcessById((int)cpid));
                    }
                    finally
                    {
                        if (ctl != null) Marshal.ReleaseComObject(ctl);
                    }
                }

                // Special rules for other files
                var ts = Process.GetProcessesByName("ts3client_win64");
                audioProcesses.AddRange(ts);

                return audioProcesses.Where(x => !string.IsNullOrEmpty(x.ProcessName) && x.ProcessName != "Idle");
            }
            finally
            {
                if (sessionEnumerator != null) Marshal.ReleaseComObject(sessionEnumerator);
                if (mgr != null) Marshal.ReleaseComObject(mgr);
                if (speakers != null) Marshal.ReleaseComObject(speakers);
                if (deviceEnumerator != null) Marshal.ReleaseComObject(deviceEnumerator);
            }
        }

        public async Task DoCleanUp()
        {
            foreach(var p in Processes)
            {
                var maxVol = await ExecuteNirsoftCommand("/GetPercent", ProcessName) / 10;
                await ExecuteNirsoftCommand("/SetVolumeChannels", ProcessName, maxVol.ToString(), maxVol.ToString());
            }
            App.Current.Shutdown();
        }

        public event PropertyChangedEventHandler? PropertyChanged;

        public void NotifyPropertyChanged([CallerMemberName] string name = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
        }
    }

    [ComImport]
    [Guid("BCDE0395-E52F-467C-8E3D-C4579291692E")]
    internal class MMDeviceEnumerator
    {
    }

    internal enum EDataFlow
    {
        eRender,
        eCapture,
        eAll,
        EDataFlow_enum_count
    }

    internal enum ERole
    {
        eConsole,
        eMultimedia,
        eCommunications,
        ERole_enum_count
    }

    [Guid("A95664D2-9614-4F35-A746-DE8DB63617E6"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    internal interface IMMDeviceEnumerator
    {
        int NotImpl1();

        [PreserveSig]
        int GetDefaultAudioEndpoint(EDataFlow dataFlow, ERole role, out IMMDevice ppDevice);

        // the rest is not implemented
    }

    [Guid("D666063F-1587-4E43-81F1-B948E807363F"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    internal interface IMMDevice
    {
        [PreserveSig]
        int Activate(ref Guid iid, int dwClsCtx, IntPtr pActivationParams, [MarshalAs(UnmanagedType.IUnknown)] out object ppInterface);

        // the rest is not implemented
    }

    [Guid("77AA99A0-1BD6-484F-8BC7-2C654C9A9B6F"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    internal interface IAudioSessionManager2
    {
        int NotImpl1();
        int NotImpl2();

        [PreserveSig]
        int GetSessionEnumerator(out IAudioSessionEnumerator SessionEnum);

        // the rest is not implemented
    }

    [Guid("E2F5BB11-0570-40CA-ACDD-3AA01277DEE8"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    internal interface IAudioSessionEnumerator
    {
        [PreserveSig]
        int GetCount(out int SessionCount);

        [PreserveSig]
        int GetSession(int SessionCount, out IAudioSessionControl Session);
    }

    [Guid("F4B1A599-7266-4319-A8CA-E70ACB11E8CD"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    internal interface IAudioSessionControl
    {
        int NotImpl1();

        [PreserveSig]
        int GetDisplayName([MarshalAs(UnmanagedType.LPWStr)] out string pRetVal);

        // the rest is not implemented
    }

    [Guid("87CE5498-68D6-44E5-9215-6DA47EF883D8"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    internal interface ISimpleAudioVolume
    {
        [PreserveSig]
        int SetMasterVolume(float fLevel, ref Guid EventContext);

        [PreserveSig]
        int GetMasterVolume(out float pfLevel);

        [PreserveSig]
        int SetMute(bool bMute, ref Guid EventContext);

        [PreserveSig]
        int GetMute(out bool pbMute);
    }
}