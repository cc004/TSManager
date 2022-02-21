using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.Loader;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;
using System.Windows.Threading;
using GameLauncher;
using Newtonsoft.Json;
using TSManager.Annotations;

namespace TSManager
{
    public class ServerContainer : INotifyPropertyChanged
    {
        private readonly string _serverName;
        private readonly string _configFile;
        private readonly ManagerConfig _managerConfig;
        private readonly Paragraph _para;
        private Process? _process;
        public bool IsRunning
        {
            get => _process != null;
            set
            {
                if (value)
                {
                    if (!IsRunning)
                    {
                        Start();
                        OnPropertyChanged(nameof(IsRunning));
                    }
                }
                else
                {
                    if (_process != null)
                    {
                        _process.Kill();
                        OnPropertyChanged(nameof(IsRunning));
                    }
                }
            }
        }

        public FlowDocument Document { get; init; }
        public event Action<ServerContainer>? OnTextChanged;
        public string Name => _serverName;

        public ServerContainer(ManagerConfig config, string serverName)
        {
            _configFile = Path.Combine(config.serverDir, serverName, config.configFile);
            if (!File.Exists(_configFile))
            {
                File.WriteAllText(_configFile, JsonConvert.SerializeObject(new ServerConfig(), Formatting.Indented));
            }

            _para = new Paragraph();
            _serverName = serverName;
            _managerConfig = config;
            Document = new FlowDocument(_para);
        }

        private void Start()
        {
            _para.Inlines.Clear();
            _process = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = typeof(GameLauncher.ServerConfig).Assembly.Location.Replace(".dll", ".exe"),
                    ArgumentList =
                    {
                        Path.GetFullPath(_configFile),
                        Path.GetFullPath(_managerConfig.pluginDir),
                        Path.GetFullPath(_managerConfig.worldDir),
                        _serverName,
                        Environment.ProcessId.ToString()
                    },
                    CreateNoWindow = true,
                    UseShellExecute = false,
                    RedirectStandardInput = true,
                    RedirectStandardOutput = true,
                    WorkingDirectory = Path.GetDirectoryName(_configFile),
                    StandardOutputEncoding = Encoding.UTF8,
                    StandardInputEncoding = Encoding.UTF8,
                },
                EnableRaisingEvents = true
            };

            _process.Exited += (_, _) =>
            {
                AddText($"---process exited with code = {_process.ExitCode}---\n");
                _process.Dispose();
                _process = null;
                OnPropertyChanged(nameof(IsRunning));
            };

            _process.OutputDataReceived += (_, args) =>
            {
                AddText($"{args.Data}\n");
            };
            _process.Start();
            _process.StandardInput.AutoFlush = true;
            _process.BeginOutputReadLine();
            _process.StandardInput.WriteLine();

            OnPropertyChanged(nameof(IsRunning));
        }
        
        private void AddText(string text)
        {
            _para.Dispatcher.Invoke(() =>
            {
                _para.Inlines.Add(text);
                if (_para.Inlines.Count > 2048)
                    for (var i = 0; i < 1024; ++i)
                        _para.Inlines.Remove(_para.Inlines.FirstInline);
                OnTextChanged?.Invoke(this);
            });
        }

        public void SendText(string msg)
        {
            if (_process == null) throw new InvalidOperationException();
            AddText($"{msg}\n");
            _process.StandardInput.WriteLine(msg);
        }

        public override string ToString()
        {
            return _serverName;
        }

        public event PropertyChangedEventHandler? PropertyChanged;

        [NotifyPropertyChangedInvocator]
        protected virtual void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}
