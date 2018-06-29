﻿using System;
using Collection = System.Collections;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using XDaggerMinerManager.ObjectModel;
using XDaggerMinerManager.Utils;
using System.Timers;


namespace XDaggerMinerManager.UI.Forms
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        private MinerManager minerManager = null;

        private ObservableCollection<MinerDataCell> minerListGridData = new ObservableCollection<MinerDataCell>();

        private bool isTimerRefreshingBusy = false;

        private static readonly string MinerStatisticsSummaryTemplate = @"当前矿机数：{0}台  上线：{1}台  下线：{2}台  主算力：{3}Mps";
        public MainWindow()
        {
            InitializeComponent();

            minerManager = MinerManager.GetInstance();
            
            InitializeUIData();
            
            //// clients.CollectionChanged += new System.Collections.Specialized.NotifyCollectionChangedEventHandler(dataChangedEvent);
        }

        private void dataChangedEvent(object sender, NotifyCollectionChangedEventArgs e)
        {
            MessageBox.Show("Added item!");
        }

        private void btnAddMiner_Click(object sender, RoutedEventArgs e)
        {
            AddMinerWizardWindow addMinerWizard = new AddMinerWizardWindow();
            addMinerWizard.MinerCreated += OnMinerCreated;
            addMinerWizard.ShowDialog();
        }

        public void OnMinerCreated(object sender, EventArgs e)
        {
            MinerCreatedEventArgs args = e as MinerCreatedEventArgs;

            if (args == null || args.CreatedMiner == null)
            {
                return;
            }

            minerManager.AddClient(args.CreatedMiner);
            RefreshMinerListGrid();
        }

        private void btnOperateMiner_Click(object sender, RoutedEventArgs e)
        {
        }

        private void InitializeUIData()
        {
            this.Title = string.Format("XDagger Miner Manager Platform ({0})", minerManager.Version);

            RefreshMinerListGrid();
        }

        private void RefreshMinerListGrid()
        {
            minerListGridData.Clear();
            foreach (MinerClient client in minerManager.ClientList)
            {
                minerListGridData.Add(new MinerDataCell(client));
            }

            int totalClient = minerManager.ClientList.Count;
            int runningClient = minerManager.ClientList.Count((client) => { return client.CurrentServiceStatus == MinerClient.ServiceStatus.Mining; });
            int stoppedClient = totalClient - runningClient;

            this.tBxClientStatisticsSummary.Text = string.Format(MinerStatisticsSummaryTemplate, totalClient, runningClient, stoppedClient, 0);
        }

        private void minerListGrid_Loaded(object sender, RoutedEventArgs e)
        {
            minerListGrid.ItemsSource = minerListGridData;
            minerListGrid.AllowDrop = false;
            minerListGrid.CanUserAddRows = false;
            minerListGrid.CanUserDeleteRows = false;
            minerListGrid.CanUserResizeRows = false;
            
            foreach (DataGridColumn col in minerListGrid.Columns)
            {
                if (col.Header.ToString() == "MinerName")
                {
                    col.Visibility = Visibility.Collapsed;
                }

                col.Header = MinerDataCell.TranslateHeaderName(col.Header.ToString());
                col.IsReadOnly = true;
            }

            minerManager.ClientStatusChanged += MinerListGrid_StatusChanged;
        }

        private void MinerListGrid_StatusChanged(object sender, EventArgs e)
        {
            this.Dispatcher.Invoke(() => RefreshMinerListGrid() );
        }

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            // Set up a timer to trigger every second.  
            Timer timer = new Timer();
            timer.Interval = 1000;
            timer.Elapsed += new ElapsedEventHandler(this.OnTimerRefresh);
            timer.Start();
            isTimerRefreshingBusy = false;
        }

        private void OnTimerRefresh(object sender, ElapsedEventArgs e)
        {
            if(isTimerRefreshingBusy)
            {
                return;
            }

            isTimerRefreshingBusy = true;

            foreach(MinerClient client in this.minerManager.ClientList)
            {
                client.RefreshStatus();
            }

            isTimerRefreshingBusy = false;
        }

        private void menuStartMiner_Click(object sender, RoutedEventArgs e)
        {
            List<MinerClient> selectedClients = GetSelectedClientsInDataGrid();
            ProgressWindow progress = new ProgressWindow("正在启动矿机...",
                () => {
                    MinerClient c = selectedClients.FirstOrDefault();
                    if (c != null)
                    {
                        ExecutionResult<OKResult> r = c.ExecuteDaemon<OKResult>("-s start");

                        if (!r.HasError)
                        {
                            c.CurrentServiceStatus = MinerClient.ServiceStatus.Disconnected;
                        }
                        else
                        {
                            /// throw new Exception(r.Code + "|" + r.ErrorMessage);
                        }
                    }
                },
                (result) => {
                    if (result.HasError)
                    {
                        MessageBox.Show("错误：" + result.Exception.ToString());
                    }

                    this.RefreshMinerListGrid();
                }
                );
            progress.ShowDialog();
        }

        private void menuStopMiner_Click(object sender, RoutedEventArgs e)
        {
            List<MinerClient> selectedClients = GetSelectedClientsInDataGrid();
            ProgressWindow progress = new ProgressWindow("正在停止矿机...",
                () => {
                    MinerClient c = selectedClients.FirstOrDefault();
                    if (c != null)
                    {
                        ExecutionResult<OKResult> r = c.ExecuteDaemon<OKResult>("-s stop");

                        if (!r.HasError)
                        {
                            c.CurrentServiceStatus = MinerClient.ServiceStatus.Stopped;
                        }
                        else
                        {
                            /// throw new Exception(r.Code + "|" + r.ErrorMessage);
                        }
                    }
                },
                (result) => {
                    if (result.HasError)
                    {
                        MessageBox.Show("错误：" + result.Exception.ToString());
                    }

                    this.RefreshMinerListGrid();
                }
                );
            progress.ShowDialog();
        }

        private void menuUninstallMiner_Click(object sender, RoutedEventArgs e)
        {
            if (MessageBox.Show("确定要卸载选定的矿机吗？", "确认", MessageBoxButton.YesNo) == MessageBoxResult.No)
            {
                return;
            }

            List<MinerClient> selectedClients = GetSelectedClientsInDataGrid();
            ProgressWindow progress = new ProgressWindow("正在卸载矿机...",
                () => {
                    MinerClient c = selectedClients.FirstOrDefault();
                    if (c != null)
                    {
                        ExecutionResult<OKResult> r = c.ExecuteDaemon<OKResult>("-s uninstall");

                        if (!r.HasError)
                        {
                            c.CurrentDeploymentStatus = MinerClient.DeploymentStatus.Downloaded;
                            c.CurrentServiceStatus = MinerClient.ServiceStatus.Stopped;
                        }
                        else
                        {
                            /// throw new Exception(r.Code + "|" + r.ErrorMessage);
                        }

                        c.DeleteBinaries();
                        minerManager.RemoveClient(c);
                    }
                },
                (result) => {
                    if (result.HasError)
                    {
                        MessageBox.Show("错误：" + result.Exception.ToString());
                    }

                    this.RefreshMinerListGrid(); }
                );
            progress.ShowDialog();
        }

        private void minerListGrid_ContextMenuOpening(object sender, ContextMenuEventArgs e)
        {
            List<MinerClient> selectedClients = GetSelectedClientsInDataGrid();

            bool containsStartedMiner = false;
            bool containsStoppedMiner = false;
            bool containsNotReadyMiner = false;

            foreach(MinerClient client in selectedClients)
            {
                containsStartedMiner |= client.IsServiceStatusRunning();
                containsStoppedMiner |= !client.IsServiceStatusRunning();
                containsNotReadyMiner |= (client.CurrentDeploymentStatus != MinerClient.DeploymentStatus.Ready);
            }

            this.menuStopMiner.IsEnabled = !containsNotReadyMiner && containsStartedMiner;
            this.menuStartMiner.IsEnabled = !containsNotReadyMiner && containsStoppedMiner;
        }

        private List<MinerClient> GetSelectedClientsInDataGrid()
        {
            List<MinerClient> selectedClients = new List<MinerClient>();
            Collection.IList selectedItems = this.minerListGrid.SelectedItems;
            
            foreach (object obj in selectedItems)
            {
                MinerDataCell cell = (MinerDataCell)obj;
                if (cell == null)
                {
                    continue;
                }

                MinerClient client = minerManager.ClientList.FirstOrDefault((c) => { return (c.MachineName == cell.MinerName); });
                if (client != null)
                {
                    selectedClients.Add(client);
                }
            }

            return selectedClients;
        }

        private void btnLockScreen_Click(object sender, RoutedEventArgs e)
        {
            ManagerInfo info = ManagerInfo.Current;
            if (!info.HasLockPassword())
            {
                SetPasswordWindow passwordWindow = new SetPasswordWindow();

                passwordWindow.ShowDialog();

                if (string.IsNullOrWhiteSpace(passwordWindow.PasswordValue))
                {
                    return;
                }

                info.SetLockPassword(passwordWindow.PasswordValue);
            }

            LockWindow lockWindow = new LockWindow(this);
            lockWindow.Show();
        }
    }


    
}
