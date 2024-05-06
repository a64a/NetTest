using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Threading;
using System;
using System.Linq;
using System.Net;
using System.Net.NetworkInformation;
using System.Threading;
using System.Threading.Tasks;

namespace NetTest
{
    public partial class MainWindow : Window
    {
        private CancellationTokenSource cts;
        private const string GoogleDnsIp = "8.8.8.8";

        public MainWindow()
        {
            InitializeComponent();
            cts = new CancellationTokenSource();
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
            try
            {
                if (ipAddress == null)
                    return;

                while (!token.IsCancellationRequested)
                {
                    using (Ping myPing = new Ping())
                    {
                        PingReply reply = await myPing.SendPingAsync(ipAddress, 1000);
                        if (reply != null)
                        {
                            await Dispatcher.UIThread.InvokeAsync(() =>
                            {
                                targetTextBlock.Text = $"Pinging {ipAddress} - Status: {reply.Status}, Time: {reply.RoundtripTime} ms, Address: {reply.Address}";
                            });
                        }
                        await Task.Delay(1000);
                    }
                }
            }
            catch (Exception ex)
            {
                if (!token.IsCancellationRequested)
                {
                    await Dispatcher.UIThread.InvokeAsync(() =>
                    {
                        targetTextBlock.Text = $"ERROR: {ex.Message}";
                    });
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
    }
}
