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
using System.Drawing;

namespace NetTest
{
    public partial class MainWindow : Window
    {
        private readonly Dictionary<string, string> serverIPs = new Dictionary<string, string>()
        {
            { "Google DNS", "8.8.8.8" },
            { "OpenDNS", "208.67.222.222" },
            { "Cloudflare DNS", "1.1.1.1" }
        };

        private DateTime sessionStartTime;
        private int totalPingsLost;
        private int totalPingsSuccessful;
        private CancellationTokenSource cts;
        private int plotIndex;
        private double[] xGateway = new double[2000];
        private double[] yGateway = new double[2000];
        private double[] xGoogleDNS = new double[2000];
        private double[] yGoogleDNS = new double[2000];
        private ScottPlot.Plottable.ScatterPlot plotGateway;
        private ScottPlot.Plottable.ScatterPlot plotGoogleDNS;
        private DateTime lastResetTime;
        private const int MaxPlotPoints = 2000;
        private const int PingInterval = 500;
        private const int ResetIntervalSeconds = 100;

#pragma warning disable CS8618
        public MainWindow()
#pragma warning restore CS8618
        {
            InitializeComponent();
            cts = new CancellationTokenSource();

            InitializePlot();

            lastResetTime = sessionStartTime = DateTime.Now;
        }

        private void InitializePlot()
        {
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
        }

        private async void StartContinuousPing(string? ipAddress, TextBlock targetTextBlock)
        {

            while (!cts.Token.IsCancellationRequested)
            {
                await Task.Delay(PingInterval);

                if (!IsNetworkConnected())
                {
                    UpdateUIText(targetTextBlock, "Not connected to any network");
                }

                using (Ping myPing = new Ping())
                {
                    try
                    {
#pragma warning disable CS8604
                        var reply = await myPing.SendPingAsync(ipAddress, 1000);
#pragma warning restore CS8604
                        if (reply != null)
                        {
                            UpdatePingStats(reply.Status == IPStatus.Success);
                            UpdateUIForPingReply(reply, targetTextBlock, ipAddress);
                        }
                    }
                    catch (PingException ex)
                    {
                        UpdateUIText(targetTextBlock, $"Error: {ex.Message}");
                        totalPingsLost++;
#pragma warning disable CS8604
                        SaveFailedPing(ipAddress, ex.Message);
#pragma warning restore CS8604
                    }
                }

                CheckAndResetPlot();
                UpdateSessionInfo();
            }
        }

        private void UpdatePingStats(bool isSuccess)
        {
            if (isSuccess)
                totalPingsSuccessful++;
            else
                totalPingsLost++;
        }

        private void UpdateUIForPingReply(PingReply reply, TextBlock targetTextBlock, string ipAddress)
        {
            Dispatcher.UIThread.InvokeAsync(() =>
            {
                string statusMessage = reply.Status == IPStatus.Success ? "Success" : "Failure";
                double packetLossPercentage = totalPingsSuccessful > 0 ? Math.Round(((double)totalPingsLost / (totalPingsSuccessful + totalPingsLost)) * 100, 2) : 0;
                targetTextBlock.Text = $"Pinging {ipAddress} - Status: {statusMessage}, Time: {reply.RoundtripTime} ms, Address: {reply.Address}, Packet loss: {packetLossPercentage}%";
                UpdatePlot(reply.RoundtripTime, ipAddress == GetDefaultGateway()?.ToString());
            });
        }

        private void UpdateSessionInfo()
        {
            TimeSpan sessionDuration = DateTime.Now - sessionStartTime;
            string sessionDurationString = sessionDuration.ToString(@"hh\:mm\:ss");
            double packetLossPercentage = totalPingsSuccessful > 0 ? Math.Round(((double)totalPingsLost / (totalPingsSuccessful + totalPingsLost)) * 100, 2) : 0;
            string sessionInfoText = $"Session duration: {sessionDurationString}" +
                $" Pings successful: {totalPingsSuccessful}" +
                $" Pings lost: {totalPingsLost}" +
                $" Packet loss: {packetLossPercentage}%";

            pingSessionInfo.Text = sessionInfoText;
        }

        private void CheckAndResetPlot()
        {
            if ((DateTime.Now - lastResetTime).TotalSeconds > ResetIntervalSeconds)
            {
                ResetPlot();
                lastResetTime = DateTime.Now;
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
        }

        private bool IsNetworkConnected()
        {
            try
            {
                return NetworkInterface.GetIsNetworkAvailable();
            }
            catch (Exception)
            {
                return false;
            }
        }

        private async void SaveFailedPing(string ipAddress, string errorMessage)
        {
            if (IsNetworkConnected())
            {
                var plt = pingPlot.Plot;
                string folderPath = "logs";
                string filename = Path.Combine(folderPath, $"FailedPings_{DateTime.Now:yyyyMMdd_HH}.txt");
                string logEntry = $"[{DateTime.Now}] Ping to {ipAddress} failed: {errorMessage}";

                double lostPingX = plotIndex / 4.0;
                double previousPingX = plotIndex > 0 ? (plotIndex - 1) / 4.0 : 0;
                var span = plt.AddHorizontalSpan(previousPingX, lostPingX);
                span.Color = System.Drawing.Color.FromArgb(100, System.Drawing.Color.Yellow);
                span.HatchColor = Color.Blue;
                span.HatchStyle = ScottPlot.Drawing.HatchStyle.SmallCheckerBoard;
                plotIndex++;
                pingPlot.Render();

                try
                {
                    if (!Directory.Exists(folderPath))
                    {
                        Directory.CreateDirectory(folderPath);
                    }

                    using (StreamWriter writer = File.AppendText(filename))
                    {
                        await writer.WriteLineAsync(logEntry);
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error saving failed ping: {ex.Message}");
                }
            }
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

            double[] xData = isGateway ? xGateway : xGoogleDNS;
            double[] yData = isGateway ? yGateway : yGoogleDNS;

            xData[plotIndex % MaxPlotPoints] = plotIndex / 4.0;
            yData[plotIndex % MaxPlotPoints] = pingTime;

            plotIndex++;
            pingPlot.Render();
        }







        private void UpdateUIText(TextBlock targetTextBlock, string text)
        {
            Dispatcher.UIThread.InvokeAsync(() =>
            {
                targetTextBlock.Text = text;
            });
        }

        private void OnStartTest(object sender, RoutedEventArgs e)
        {
            cts?.Cancel();
            cts = new CancellationTokenSource();

#pragma warning disable CS8602
#pragma warning disable CS8600
            var selectedServer = ((ComboBoxItem)serverSelector.SelectedItem).Content.ToString();
#pragma warning restore CS8600
#pragma warning restore CS8602
#pragma warning disable CS8604
            var ipAddress = serverIPs[selectedServer];
#pragma warning restore CS8604

            string? gatewayAddress = GetDefaultGateway()?.ToString();
            if (string.IsNullOrEmpty(gatewayAddress))
            {
                UpdateUIText(messageGateway, "Not connected to any network");
            }
            else
            {
                StartContinuousPing(ipAddress, messageGoogleDNS);
                StartContinuousPing(gatewayAddress, messageGateway);
            }

        }

        private void OnStopTest(object sender, RoutedEventArgs e)
        {
            cts?.Cancel();
        }

        private async void OnSendRapport(object sender, RoutedEventArgs e)
        {

            double packetLossPercentage = totalPingsSuccessful > 0 ? Math.Round(((double)totalPingsLost / (totalPingsSuccessful + totalPingsLost)) * 100, 2) : 0;
            string rapport = $"Session duration: {(DateTime.Now - sessionStartTime).ToString(@"hh\:mm\:ss")}" +
                $" Pings successful: {totalPingsSuccessful}" +
                $" Pings lost: {totalPingsLost}" +
                $" Packet loss: {packetLossPercentage}%";

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
        }
    }
}