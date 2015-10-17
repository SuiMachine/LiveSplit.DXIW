using System;
using System.Collections.Generic;
using System.Linq;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using LiveSplit.ComponentUtil;

namespace LiveSplit.DXIW
{
    class GameMemory
    {
        public bool isSteam;

        public event EventHandler OnFirstLevelLoading;
        public event EventHandler OnPlayerGainedControl;
        public event EventHandler OnLoadStarted;
        public event EventHandler OnLoadFinished;

        private Task _thread;
        private CancellationTokenSource _cancelSource;
        private SynchronizationContext _uiThread;
        private List<int> _ignorePIDs;
        private DXIWSettings _settings;

        private DeepPointer _isLoadingPtr;
        const int baseAddress = 0x00400000;

        private enum ExpectedDllSizes
        {
            GOG = 6922240,
            Steam = 6930432
        }

        public bool[] splitStates { get; set; }

        public GameMemory(DXIWSettings componentSettings)
        {
            _settings = componentSettings;

            _ignorePIDs = new List<int>();
        }

        public void StartMonitoring()
        {
            if (_thread != null && _thread.Status == TaskStatus.Running)
            {
                throw new InvalidOperationException();
            }
            if (!(SynchronizationContext.Current is WindowsFormsSynchronizationContext))
            {
                throw new InvalidOperationException("SynchronizationContext.Current is not a UI thread.");
            }

            _uiThread = SynchronizationContext.Current;
            _cancelSource = new CancellationTokenSource();
            _thread = Task.Factory.StartNew(MemoryReadThread);
        }

        public void Stop()
        {
            if (_cancelSource == null || _thread == null || _thread.Status != TaskStatus.Running)
            {
                return;
            }

            _cancelSource.Cancel();
            _thread.Wait();
        }

        bool isLoading = false;
        bool prevIsLoading = false;
        bool loadingStarted = false;
        uint delay = 62;

        void MemoryReadThread()
        {
            Debug.WriteLine("[NoLoads] MemoryReadThread");

            while (!_cancelSource.IsCancellationRequested)
            {
                try
                {
                    Debug.WriteLine("[NoLoads] Waiting for dx2main.exe...");

                    Process game;
                    while ((game = GetGameProcess()) == null)
                    {
                        delay = 90;
                        Thread.Sleep(250);
                        if (_cancelSource.IsCancellationRequested)
                        {
                            return;
                        }

                        isLoading = true;
                        if(isLoading != prevIsLoading)
                        {
                            loadingStarted = true;

                            // pause game timer
                            _uiThread.Post(d =>
                            {
                                if (this.OnLoadStarted != null)
                                {
                                    this.OnLoadStarted(this, EventArgs.Empty);
                                }
                            }, null);
                        }

                        prevIsLoading = true;

                    }

                    Debug.WriteLine("[NoLoads] Got games process!");

                    uint frameCounter = 0;

                    while (!game.HasExited)
                    {
                        if(delay == 0)
                        {
                            if(_settings.UseNonSafeMemoryReading)
                            {   //You've seen nothing!!!!!
                                if(isSteam)
                                    isLoading = Convert.ToBoolean(Trainer.ReadByte(game, baseAddress + 0x5EB9A0));
                                else
                                    isLoading = Convert.ToBoolean(Trainer.ReadByte(game, baseAddress + 0x5ED9B0));
                            }
                            else
                                _isLoadingPtr.Deref(game, out isLoading);

                            if (isLoading != prevIsLoading)
                            {
                                if (isLoading)
                                {
                                    Debug.WriteLine(String.Format("[NoLoads] Load Start - {0}", frameCounter));

                                    loadingStarted = true;

                                    // pause game timer
                                    _uiThread.Post(d =>
                                    {
                                        if (this.OnLoadStarted != null)
                                        {
                                            this.OnLoadStarted(this, EventArgs.Empty);
                                        }
                                    }, null);
                                }
                                else
                                {
                                    Debug.WriteLine(String.Format("[NoLoads] Load End - {0}", frameCounter));

                                    if (loadingStarted)
                                    {
                                        loadingStarted = false;

                                        // unpause game timer
                                        _uiThread.Post(d =>
                                        {
                                            if (this.OnLoadFinished != null)
                                            {
                                                this.OnLoadFinished(this, EventArgs.Empty);
                                            }
                                        }, null);
                                    }
                                }
                            }

                            prevIsLoading = isLoading;
                            frameCounter++;

                            Thread.Sleep(15);

                            if (_cancelSource.IsCancellationRequested)
                            {
                                return;
                            }
                        }
                        else
                        {
                            prevIsLoading = isLoading;
                            frameCounter++;
                            delay--;
                            Thread.Sleep(15);
                        }
                    }

                    // pause game timer on exit or crash
                    _uiThread.Post(d =>
                    {
                        if (this.OnLoadStarted != null)
                        {
                            this.OnLoadStarted(this, EventArgs.Empty);
                        }
                    }, null);
                    isLoading = true;
                }
                catch (Exception ex)
                {
                    Debug.WriteLine(ex.ToString());
                    Thread.Sleep(1000);
                }
            }
        }


        Process GetGameProcess()
        {
            Process game = Process.GetProcesses().FirstOrDefault(p => (p.ProcessName.ToLower() == "dx2main") && !p.HasExited && !_ignorePIDs.Contains(p.Id));
            if (game == null)
            {
                return null;
            }

            if (game.MainModuleWow64Safe().ModuleMemorySize == (int)ExpectedDllSizes.Steam)
            {
                isSteam = true;
                _isLoadingPtr = new DeepPointer(0x5EB9A0);  // == 1 if a loadscreen is happening
            }
            else if(game.MainModuleWow64Safe().ModuleMemorySize == (int)ExpectedDllSizes.GOG)  //GOG version
            {
                isSteam = false;
                _isLoadingPtr = new DeepPointer(0x5ED9B0);  // == 1 if a loadscreen is happening
            }
            else
            {
                _ignorePIDs.Add(game.Id);
                _uiThread.Send(d => MessageBox.Show("Unexpected game version. DXIW Steam or GOG is required.", "LiveSplit.DXIW",
                    MessageBoxButtons.OK, MessageBoxIcon.Error), null);
                return null;
            }

            return game;
        }
    }
}
