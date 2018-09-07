﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using XDaggerMinerManager.Utils;
using Newtonsoft.Json;

namespace XDaggerMinerManager.ObjectModel
{
    /// <summary>
    /// MineClient Class
    /// </summary>
    public class MinerClient
    {
        private bool hasStatusChanged = false;

        public enum DeploymentStatus
        {
            Unknown = -1,
            NotExist = 1,
            Downloaded = 2,
            PrerequisitesInstalled = 3,
            Ready = 4,
        }

        public enum ServiceStatus
        {
            Unknown = 0,
            NotInstalled = 10,
            Stopped = 20,
            Initializing = 30,
            Disconnected = 40,
            Connected = 50,
            Mining = 60,
        }

        public event EventHandler StatusChanged;
        
        public void ResetStatusChanged()
        {
            this.hasStatusChanged = false;
        }

        public MinerClient()
        {

        }

        public MinerClient(string machineName, string deploymentFolder, string version = "", string instanceName = "")
        {
            this.Machine = new MinerMachine() {
                FullMachineName = machineName.Trim().ToUpper()
            };
            
            this.DeploymentFolder = deploymentFolder.Trim().ToLower();
            this.Version = version;
            this.InstanceName = instanceName;

            this.CurrentDeploymentStatus = DeploymentStatus.Unknown;
            this.CurrentServiceStatus = ServiceStatus.Unknown;
        }

        public MinerClient(MinerMachine machine, string deploymentFolder, string version = "", string instanceName = "")
        {
            this.Machine = machine;

            this.DeploymentFolder = deploymentFolder.Trim().ToLower();
            this.Version = version;
            this.InstanceName = instanceName;

            this.CurrentDeploymentStatus = DeploymentStatus.Unknown;
            this.CurrentServiceStatus = ServiceStatus.Unknown;
        }

        public string Name
        {
            get
            {
                if (string.IsNullOrEmpty(this.InstanceName))
                {
                    return this.Machine?.FullMachineName;
                }
                else
                {
                    return string.Format("{0}_{1}", this.Machine?.FullMachineName, this.InstanceName);
                }
            }
        }
        
        [JsonProperty(PropertyName = "machine")]
        public MinerMachine Machine
        {
            get; set;
        }

        [JsonProperty(PropertyName = "xdagger_wallet_address")]
        public string XDaggerWalletAddress
        {
            get; set;
        }

        [JsonProperty(PropertyName = "eth_full_pool_address")]
        public string EthFullPoolAddress
        {
            get; set;
        }

        [JsonProperty(PropertyName = "instance_name")]
        public string InstanceName
        {
            get; set;
        }

        [JsonProperty(PropertyName = "instance_type")]
        public string InstanceType
        {
            get; set;
        }

        public string BinaryPath
        {
            get
            {
                return System.IO.Path.Combine(this.DeploymentFolder, WinMinerReleaseBinary.ProjectName);
            }
        }

        public bool IsServiceStatusRunning()
        {
            return this.CurrentServiceStatus > ServiceStatus.Stopped;
        }

        [JsonProperty(PropertyName = "deployment_folder")]
        public string DeploymentFolder
        {
            get; private set;
        }

        private DeploymentStatus currentDeploymentStatus = DeploymentStatus.Unknown;

        [JsonProperty(PropertyName = "current_deployment_status")]
        public DeploymentStatus CurrentDeploymentStatus
        {
            get
            {
                return this.currentDeploymentStatus;
            }
            set
            {
                if (value != this.currentDeploymentStatus)
                {
                    this.currentDeploymentStatus = value;
                    hasStatusChanged = true;
                    this.OnStatusChanged(EventArgs.Empty);
                }
                else
                {
                    this.currentDeploymentStatus = value;
                }
            }
        }

        public ServiceStatus currentServiceStatus;

        [JsonProperty(PropertyName = "current_service_status")]
        public ServiceStatus CurrentServiceStatus
        {
            get
            {
                return this.currentServiceStatus;
            }

            set
            {
                if (value != this.currentServiceStatus)
                {
                    this.currentServiceStatus = value;
                    hasStatusChanged = true;
                    this.OnStatusChanged(EventArgs.Empty);
                }
                else
                {
                    this.currentServiceStatus = value;
                }
            }
        }

        private double currentHashRate = 0;

        public double CurrentHashRate
        {
            get
            {
                return currentHashRate;
            }
            private set
            {
                if (value != this.currentHashRate)
                {
                    this.currentHashRate = value;
                    hasStatusChanged = true;
                    this.OnStatusChanged(EventArgs.Empty);
                }
                else
                {
                    this.currentHashRate = value;
                }
            }
        }

        [JsonProperty(PropertyName = "device")]
        public MinerDevice Device
        {
            get; set;
        }

        [JsonProperty(PropertyName = "version")]
        public string Version
        {
            get; set;
        }

        
        public string GetRemoteDeploymentPath()
        {
            if (string.IsNullOrEmpty(this.Machine?.FullMachineName) || string.IsNullOrEmpty(DeploymentFolder))
            {
                return string.Empty;
            }

            return string.Format("\\\\{0}\\{1}", this.Machine?.FullMachineName, this.DeploymentFolder.Replace(":", "$"));
        }

        public string GetRemoteBinaryPath()
        {
            if (string.IsNullOrEmpty(this.Machine?.FullMachineName) || string.IsNullOrEmpty(BinaryPath))
            {
                return string.Empty;
            }

            return string.Format("\\\\{0}\\{1}", this.Machine?.FullMachineName, this.BinaryPath.Replace(":", "$"));
        }

        public string GetRemoteTempPath()
        {
            if (string.IsNullOrEmpty(this.Machine?.FullMachineName))
            {
                return string.Empty;
            }

            return string.Format("\\\\{0}\\{1}", this.Machine?.FullMachineName, System.IO.Path.GetTempPath().Replace(":", "$"));
        }

        public T ExecuteDaemon<T>(string parameters)
        {
            TargetMachineExecutor executor = TargetMachineExecutor.GetExecutor(this.Machine);
            string daemonFullPath = System.IO.Path.Combine(this.BinaryPath, WinMinerReleaseBinary.DaemonExecutionFileName);

            return executor.ExecuteCommandAndThrow<T>(daemonFullPath, parameters);
        }

        public void DeleteBinaries()
        {
            if (this.CurrentDeploymentStatus < DeploymentStatus.Downloaded)
            {
                // There should not be binaries, just ignore
                this.CurrentDeploymentStatus = DeploymentStatus.NotExist;
                return;
            }

            try
            {
                string binariesPath = GetRemoteBinaryPath();

                if (!string.IsNullOrEmpty(binariesPath))
                {
                    System.IO.Directory.Delete(binariesPath, true);
                }

                this.CurrentDeploymentStatus = MinerClient.DeploymentStatus.NotExist;
            }
            catch(Exception ex)
            {
                throw ex;
            }
        }

        /// <summary>
        /// Update the current deployment status and service status
        /// </summary>
        public bool RefreshStatus()
        {
            try
            {
                ReportOutput report = this.ExecuteDaemon<ReportOutput>("-r");
                if (report == null)
                {
                    if (hasStatusChanged)
                    {
                        return false;
                    }
                    else
                    {
                        hasStatusChanged = true;
                        return true;
                    }
                }

                this.CurrentServiceStatus = (ServiceStatus)report.Status;

                if (this.CurrentServiceStatus == ServiceStatus.Mining)
                {
                    this.CurrentHashRate = report.HashRate;
                }
                else
                {
                    this.CurrentHashRate = 0;
                }

                hasStatusChanged = true;
            }
            catch(Exception ex)
            {
                hasStatusChanged = false;
            }

            return hasStatusChanged;
        }

        #region Private Methods
        

        private void OnStatusChanged(EventArgs e)
        {
            StatusChanged?.Invoke(this, e);
        }

        #endregion




    }
}
