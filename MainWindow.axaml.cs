using ScottPlot;
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
using System.Drawing;

namespace NetTest
{
    public partial class MainWindow : Window
    {
        private CancellationTokenSource cts;
        private const string GoogleDnsIp = "8.8.8.8";
        private int plotIndex = 0;
        private double[] xGateway = new double[100];
        private double[] yGateway = new double[100];
        private double[] xGoogleDNS = new double[100];
        private double[] yGoogleDNS = new double[100];
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
            plt.Style(Style.Black);
            plt.Title("Ms[s]");
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
        }

        private void OnStartTest(object sender, RoutedEventArgs e)
        {
            networkTestButton.Content = "Stop Network Test";
            cts = new CancellationTokenSource();
            string? gatewayAddress = GetDefaultGateway()?.ToString();
            if (string.IsNullOrEmpty(gatewayAddress))
            {
                messageGateway.Text = "Not connected to any network";
            }
            else
            {
                StartContinuousPing(GoogleDnsIp, cts.Token, messageGoogleDNS);
                StartContinuousPing(gatewayAddress, cts.Token, messageGateway);
            }
        }

        private void OnStopTest(object sender, RoutedEventArgs e)
        {
            networkTestButton.Content = "Start Network Test";
            cts.Cancel();
        }

        private async void StartContinuousPing(string? ipAddress, CancellationToken token, TextBlock targetTextBlock)
        {
            while (!token.IsCancellationRequested)
            {
                using (Ping myPing = new Ping())
                {
                    try
                    {
                        PingReply reply = await myPing.SendPingAsync(ipAddress, 1000);
                        if (reply != null)
                        {
                            await Dispatcher.UIThread.InvokeAsync(() =>
                            {
                                targetTextBlock.Text = $"Pinging {ipAddress} - Status: {reply.Status}, Time: {reply.RoundtripTime} ms, Address: {reply.Address}";
                                UpdatePlot(reply.RoundtripTime, ipAddress == GetDefaultGateway()?.ToString());
                            });
                        }
                        await Task.Delay(500); // Changed to 500ms for plotting every 0.5 seconds
                    }
                    catch (PingException ex)
                    {
                        // Handle network connection errors
                        await Dispatcher.UIThread.InvokeAsync(() =>
                        {
                            targetTextBlock.Text = $"Error: {ex.Message}";
                        });

                        // Save failed ping to a file
                        SaveFailedPing(ipAddress, ex.Message);
                        
                        // Wait before retrying
                        await Task.Delay(5000); // Delay for 5 seconds before retrying
                    }
                }
                if ((DateTime.Now - lastResetTime).TotalSeconds > 100)
                {
                    var plt = pingPlot.Plot;
                    plt.Clear();
                    ResetPlot();
                    lastResetTime = DateTime.Now;
                }
            }
        }

        private void ResetPlot()
        {
            var plt = pingPlot.Plot;
            plt.Grid(enable: true);
            plt.YAxis.SetBoundary(0, 1000);
            plt.XAxis.SetBoundary(0, 100);
            plt.Style(Style.Black);
            plt.Title("Ms[s]");
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

            plotIndex = 0;
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
            string filename = $"FailedPings_{DateTime.Now:yyyyMMdd_HH}.txt";
            string logEntry = $"[{DateTime.Now}] Ping to {ipAddress} failed: {errorMessage}";

            try
            {
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
