using System;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.NetworkInformation;
using System.Threading;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Threading;
using System.Collections.Generic;
using ScottPlot;
using System.Drawing.Printing;

namespace NetTest
{
    public partial class MainWindow : Window
    {
        private Dictionary<string, string> serverIPs = new Dictionary<string, string>()
        {
            { "Google DNS", "8.8.8.8" },
            { "OpenDNS", "208.67.222.222" },
            { "Cloudflare DNS", "1.1.1.1" }
        };

        private DateTime sessionStartTime;
        private int totalPingsSent = 0;
        private int totalPingsLost = 0;
        private int totalPingsReceived = 0;
        private CancellationTokenSource cts;
        private const string GoogleDnsIp = "8.8.8.8";
        private int plotIndex = 0;
        private double[] xGateway = new double[2000];
        private double[] yGateway = new double[2000];
        private double[] xGoogleDNS = new double[2000];
        private double[] yGoogleDNS = new double[2000];
        private ScottPlot.Plottable.ScatterPlot plotGateway;
        private ScottPlot.Plottable.ScatterPlot plotGoogleDNS;
        private DateTime lastResetTime;

        public MainWindow()
        {
            InitializeComponent();
            cts = new CancellationTokenSource();

            var plt = pingPlot.Plot;
            plt.Grid(enable: true);
            plt.YAxis.SetBoundary(0, 1000);
            plt.XAxis.SetBoundary(0, 100);
            plt.SetAxisLimits(xMin: 0, xMax: 100, yMin: 0, yMax: 1000);
            plt.Style(Style.Gray2);
            plt.Title("ms[s]");
            plt.XLabel("Access time");
            plt.YLabel("Time");
            plt.Legend(true);

            var legend = plt.Legend();
            plotGateway = plt.AddScatter(xGateway, yGateway, label: "Gateway", color: System.Drawing.Color.Red, lineWidth: 0);
            plotGoogleDNS = plt.AddScatter(xGoogleDNS, yGoogleDNS, label: "Google DNS", color: System.Drawing.Color.Blue, lineWidth: 0);
            legend.FontColor = System.Drawing.Color.White;
            legend.FillColor = System.Drawing.Color.Black;
            legend.Location = Alignment.UpperCenter;
            legend.Orientation = Orientation.Horizontal;
            plt.Legend();

            lastResetTime = DateTime.Now;
            sessionStartTime = DateTime.Now;
        }

        private void OnStartTest(object sender, RoutedEventArgs e)
        {
            cts = new CancellationTokenSource();

            var selectedServer = ((ComboBoxItem)serverSelector.SelectedItem).Content.ToString();
            var ipAddress = serverIPs[selectedServer];

            string? gatewayAddress = GetDefaultGateway()?.ToString();
            if (string.IsNullOrEmpty(gatewayAddress))
            {
                messageGateway.Text = "Not connected to any network";
            }
            else
            {
                StartContinuousPing(ipAddress, cts.Token, messageGoogleDNS);
                StartContinuousPing(gatewayAddress, cts.Token, messageGateway);
            }

            resetButton.IsEnabled = true;  // Enable the reset button
        }

        private void OnStopTest(object sender, RoutedEventArgs e)
        {
            cts.Cancel();
            resetButton.IsEnabled = false;
        }

        private void OnReset(object sender, RoutedEventArgs e)
        {
            if (cts != null && !cts.IsCancellationRequested)
            {
                ResetPlot();

                sessionStartTime = DateTime.Now;
                totalPingsSent = 0;
                totalPingsLost = 0;
                totalPingsReceived = 0;

                var selectedServer = ((ComboBoxItem)serverSelector.SelectedItem).Content.ToString();
                var ipAddress = serverIPs[selectedServer];
                string? gatewayAddress = GetDefaultGateway()?.ToString();
                if (string.IsNullOrEmpty(gatewayAddress))
                {
                    messageGateway.Text = "Not connected to any network";
                }
                else
                {
                    StartContinuousPing(ipAddress, cts.Token, messageGoogleDNS);
                    StartContinuousPing(gatewayAddress, cts.Token, messageGateway);
                }
            }
        }

        private void UpdateSessionInfo()
        {
            TimeSpan sessionDuration = DateTime.Now - sessionStartTime;
            string sessionDurationString = sessionDuration.ToString(@"hh\:mm\:ss");
            string sessionInfoText = $"Session duration: {sessionDurationString}" +
                $" Pings sent: {totalPingsSent}" +
                $" Pings lost: {totalPingsLost}" +
                $" Packet loss: {((double)totalPingsLost / totalPingsSent) * 100}%";

            pingSessionInfo.Text = sessionInfoText;
        }


        private async void OnSendRapport(object sender, RoutedEventArgs e)
        {
            string rapport = $"Session duration: {(DateTime.Now - sessionStartTime).ToString(@"hh\:mm\:ss")}" +
                $" Pings sent: {totalPingsSent}" +
                $" Pings lost: {totalPingsLost}" +
                $" Packet loss: {((double)totalPingsLost / totalPingsSent) * 100}%";

            string folderPath = "logs";
            string filename = Path.Combine(folderPath, $"FailedPings_{DateTime.Now:yyyyMMdd_HH}.txt");

            try
            {
                if (!Directory.Exists(folderPath))
                {
                    Directory.CreateDirectory(folderPath);
                }

                using (StreamWriter writer = File.AppendText(filename))
                {
                    await writer.WriteLineAsync($"# Rapport generated at {DateTime.Now}");
                    await writer.WriteLineAsync($"# {rapport}");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error saving rapport: {ex.Message}");
            }

            cts.Cancel();
            resetButton.IsEnabled = false;
        }


        private async void StartContinuousPing(string? ipAddress, CancellationToken token, TextBlock targetTextBlock)
        {
            while (!token.IsCancellationRequested)
            {
                using (Ping myPing = new Ping())
                {
                    try
                    {
                        totalPingsSent++;

                        PingReply reply = await myPing.SendPingAsync(ipAddress, 1000);
                        if (reply != null)
                        {
                            UpdatePingStats(reply.Status == IPStatus.Success);

                            await Dispatcher.UIThread.InvokeAsync(() =>
                            {
                                string statusMessage = reply.Status == IPStatus.Success ? "Success" : "Failure";
                                targetTextBlock.Text = $"Pinging {ipAddress} - Status: {statusMessage}, Time: {reply.RoundtripTime} ms, Address: {reply.Address}, Packet Loss: {((double)totalPingsLost / totalPingsSent) * 100}%";
                                UpdatePlot(reply.RoundtripTime, ipAddress == GetDefaultGateway()?.ToString());
                            });
                        }
                        await Task.Delay(500);
                    }
                    catch (PingException ex)
                    {
                        await Dispatcher.UIThread.InvokeAsync(() =>
                        {
                            targetTextBlock.Text = $"Error: {ex.Message}";
                        });

                        SaveFailedPing(ipAddress, ex.Message);

                        await Task.Delay(500);
                    }
                }
                if ((DateTime.Now - lastResetTime).TotalSeconds > 100)
                {
                    ResetPlot();
                    lastResetTime = DateTime.Now;
                }

                UpdateSessionInfo();
            }
        }




        private void UpdatePingStats(bool isSuccess)
        {
            totalPingsSent++;
            if (!isSuccess)
            {
                totalPingsLost++;
            }
            else
            {
                totalPingsReceived++;
            }
        }

        private void ResetPlot()
        {
            var plt = pingPlot.Plot;
            plt.Clear();

            Array.Clear(xGateway, 0, xGateway.Length);
            Array.Clear(yGateway, 0, yGateway.Length);
            Array.Clear(xGoogleDNS, 0, xGoogleDNS.Length);
            Array.Clear(yGoogleDNS, 0, yGoogleDNS.Length);
            plt.Grid(enable: true);
            plt.YAxis.SetBoundary(0, 1000);
            plt.XAxis.SetBoundary(0, 100);
            plt.SetAxisLimits(xMin: 0, xMax: 100, yMin: 0, yMax: 1000);
            plt.Style(Style.Gray2);
            plt.Title("ms[s]");
            plt.XLabel("Access time");
            plt.YLabel("Time");
            plt.Legend(true);

            var legend = plt.Legend();
            plotIndex = 0;
            plotGateway = plt.AddScatter(xGateway, yGateway, label: "Gateway", color: System.Drawing.Color.Red, lineWidth: 0);
            plotGoogleDNS = plt.AddScatter(xGoogleDNS, yGoogleDNS, label: "Google DNS", color: System.Drawing.Color.Blue, lineWidth: 0);
            legend.FontColor = System.Drawing.Color.White;
            legend.FillColor = System.Drawing.Color.Black;
            legend.Location = Alignment.UpperCenter;
            legend.Orientation = Orientation.Horizontal;
            plt.Legend();
            lastResetTime = DateTime.Now;
        }


        private IPAddress? GetDefaultGateway()
        {
            try
            {
                return NetworkInterface.GetAllNetworkInterfaces()
                    .Where(n => n.OperationalStatus == OperationalStatus.Up)
                    .Where(n => n.NetworkInterfaceType != NetworkInterfaceType.Loopback)
                    .SelectMany(n => n.GetIPProperties().GatewayAddresses)
                    .Select(g => g.Address)
                    .FirstOrDefault(a => a?.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork);
            }
            catch (Exception)
            {
                return null;
            }
        }

        private void UpdatePlot(double pingTime, bool isGateway)
        {
            var plt = pingPlot.Plot;

            if (isGateway)
            {
                xGateway[plotIndex % xGateway.Length] = plotIndex / 4.0;
                yGateway[plotIndex % yGateway.Length] = pingTime;
            }
            else
            {
                xGoogleDNS[plotIndex % xGoogleDNS.Length] = plotIndex / 4.0;
                yGoogleDNS[plotIndex % yGoogleDNS.Length] = pingTime;
            }

            plotIndex++;
            pingPlot.Render();
        }

        private void SaveFailedPing(string ipAddress, string errorMessage)
        {
            string folderPath = "logs";
            string filename = Path.Combine(folderPath, $"FailedPings_{DateTime.Now:yyyyMMdd_HH}.txt");
            string logEntry = $"[{DateTime.Now}] Ping to {ipAddress} failed: {errorMessage}";

            try
            {
                if (!Directory.Exists(folderPath))
                {
                    Directory.CreateDirectory(folderPath);
                }

                using (StreamWriter writer = File.AppendText(filename))
                {
                    writer.WriteLine(logEntry);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error saving failed ping: {ex.Message}");
            }
        }
    }
}
