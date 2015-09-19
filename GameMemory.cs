using System;
using System.Collections.Generic;
using System.Linq;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace LiveSplit.DXIW
{
    class GameMemory
    {
        public event EventHandler OnLoadStarted;
        public event EventHandler OnLoadFinished;

        private Task _thread;
        private CancellationTokenSource _cancelSource;
        private SynchronizationContext _uiThread;
        private List<int> _ignorePIDs;

        private DeepPointer _IsLoading;

        private enum ExpectedDllSizes
        {
            DXIWGOG = 6922240,
            DXIWSteam = 6930432,
        }

        public void resetSplitStates()
        {
        }

        public GameMemory(DXIWSettings componentSettings)
        {
            resetSplitStates();

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

        void MemoryReadThread()
        {
            Trace.WriteLine("[NoLoads] MemoryReadThread");

            while (!_cancelSource.IsCancellationRequested)
            {
                try
                {
                    bool isLoading;
                    bool prevIsLoading = false;
                    bool loadingStarted = false;
                    uint simpleDelay = 62;                                                                                   //Counts down 62*15ms before it states there is no loading

                    Trace.WriteLine("[NoLoads] Waiting for DX2Main.exe...");
                    uint frameCounter = 0;
                    
                    Process game;
                    while ((game = GetGameProcess()) == null)
                    {
                        isLoading = true;                                                                                   //Required, because of the game killing process during loadings.

                        if (isLoading != prevIsLoading)
                        {
                            if (isLoading)
                            {
                                Trace.WriteLine(String.Format("[NoLoads] Load Start - {0}", frameCounter));

                                loadingStarted = true;

                                // pause game timer
                                _uiThread.Post(d =>
                                {
                                    if (this.OnLoadStarted != null)
                                    {
                                        this.OnLoadStarted(this, EventArgs.Empty);
                                    }
                                }, null);
                                simpleDelay = 62;
                                Trace.WriteLine("[NoLoads] Loadings, thread delay 62.");
                            }
                        }

                        Thread.Sleep(250);
                        if (_cancelSource.IsCancellationRequested)
                        {
                            return;
                        }

                        prevIsLoading = isLoading;
                    }

                    Trace.WriteLine("[NoLoads] Got games process!");

                    while (!game.HasExited)
                    {
                        _IsLoading.Deref(game, out isLoading);
                        if(simpleDelay==0)
                        {
                            if (isLoading != prevIsLoading)
                            {
                                if (isLoading)
                                {
                                    Trace.WriteLine(String.Format("[NoLoads] Load Start - {0}", frameCounter));

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
                                    Trace.WriteLine(String.Format("[NoLoads] Load End - {0}", frameCounter));

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
                        }
                        else
                        {
                            simpleDelay--;
                        }
                        prevIsLoading = isLoading;

                        frameCounter++;

                        Thread.Sleep(15);

                        if (_cancelSource.IsCancellationRequested)
                        {
                            return;
                        }
                    }
                }
                catch (Exception ex)
                {
                    Trace.WriteLine(ex.ToString());
                    Thread.Sleep(1000);
                }
            }
        }

        Process GetGameProcess()
        {
            Process game = Process.GetProcesses().FirstOrDefault(p => p.ProcessName.ToLower() == "dx2main" && !p.HasExited && !_ignorePIDs.Contains(p.Id));
            if (game == null)
            {
                return null;
            }

            if (game.MainModule.ModuleMemorySize != (int)ExpectedDllSizes.DXIWSteam && game.MainModule.ModuleMemorySize != (int)ExpectedDllSizes.DXIWGOG)
            {
                _ignorePIDs.Add(game.Id);
                _uiThread.Send(d => MessageBox.Show("Unexpected game version. Deus Ex Invisible War (1.2) on Steam or GOG is required.", "LiveSplit.DXIW",
                    MessageBoxButtons.OK, MessageBoxIcon.Error), null);
                return null;
            }
            else if (game.MainModule.ModuleMemorySize == (int)ExpectedDllSizes.DXIWSteam)
            {
                _IsLoading = new DeepPointer(0x5EB9A0);
            }
            else if (game.MainModule.ModuleMemorySize == (int)ExpectedDllSizes.DXIWGOG)
            {
                _IsLoading = new DeepPointer(0x5ED9B0);
            }

            return game;
        }
    }
}
