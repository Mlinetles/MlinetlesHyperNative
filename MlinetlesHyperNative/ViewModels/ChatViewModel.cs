namespace MlinetlesHyperNative.ViewModels
{
    public class ChatViewModel : ViewModelBase
    {
        private byte[]? key = null;

        private string? publicIP = null;

        private readonly Pipe receive = new();

        private NetworkStream? stream = null;

        private readonly CancellationTokenSource cancel = new();

        private bool connected = false;

        private string? input = null;

        public ChatViewModel()
        {
            GetPublicIP();

            OnReceive();

            ConnectFromClipboard = ReactiveCommand.CreateFromTask(async (Button button) =>
            {
                var clipboard = TopLevel.GetTopLevel(button)?.Clipboard;

                if (clipboard is null) return;

                var result = await clipboard.GetTextAsync();

                if (result is null) return;

                ConnectInfo? info;

                try
                {
                    info = JsonSerializer.Deserialize<ConnectInfo>(result);
                }
                catch
                {
                    return;
                }

                if (info is null) return;

                using var client = new TcpClient();

                try
                {
                    await client.ConnectAsync(info.IP, info.Port);
                }
                catch (SocketException)
                {
                    return;
                }

                Connected = true;

                stream = client.GetStream();

                await StartConnectAsync();

                while (!cancel.IsCancellationRequested && client.Connected)
                {
                    try
                    {
                        var length = await stream.ReadAsync(receive.Writer.GetMemory());

                        if (length == 0)
                        {
                            await stream.WriteAsync([1], 0, 1);
                            if (!client.Connected) break;
                        }

                        receive.Writer.Advance(length);

                        await receive.Writer.FlushAsync();
                    }
                    catch (IOException e) when (e.InnerException is SocketException exc && exc.NativeErrorCode != 10035)
                    {
                        break;
                    }
                }

                Disconnect();
            });

            WaitForConnect = ReactiveCommand.CreateFromTask(async (Button button) =>
            {
                var clipboard = TopLevel.GetTopLevel(button)?.Clipboard ?? throw new NullReferenceException();

                using var listener = new TcpListener(IPAddress.Any, 0);

                listener.Start();

                await clipboard.SetTextAsync(JsonSerializer.Serialize<ConnectInfo>(new(PublicIP!, ((IPEndPoint)listener.LocalEndpoint).Port)));

                using var client = await listener.AcceptTcpClientAsync();

                listener.Stop();

                Connected = true;

                stream = client.GetStream();

                await StartConnectAsync();

                while (!cancel.IsCancellationRequested && client.Connected)
                {
                    try
                    {
                        var length = await stream.ReadAsync(receive.Writer.GetMemory());

                        if (length == 0)
                        {
                            await stream.WriteAsync([1], 0, 1);
                            if (!client.Connected) break;
                        }

                        if (cancel.IsCancellationRequested) break;

                        receive.Writer.Advance(length);

                        await receive.Writer.FlushAsync();
                    }
                    catch (IOException e) when (e.InnerException is SocketException exc && exc.NativeErrorCode != 10035)
                    {
                        break;
                    }
                }
                Disconnect();
            }, this.WhenAnyValue(x => x.PublicIP, x => !string.IsNullOrEmpty(x)));

            Send = ReactiveCommand.CreateFromTask(async () =>
            {
                await OnSend(new Message(Input!));

                Input = string.Empty;
            }, this.WhenAnyValue(x => x.Connected));
        }

        public string? PublicIP { get => publicIP; set => this.RaiseAndSetIfChanged(ref publicIP, value); }

        public bool Connected { get => connected; set => this.RaiseAndSetIfChanged(ref connected, value); }

        public string? Input { get => input; set => this.RaiseAndSetIfChanged(ref input, value); }

        public ObservableCollection<Message> Messages { get; set; } = new();

        public ReactiveCommand<Button, Unit> WaitForConnect { get; }

        public ReactiveCommand<Button, Unit> ConnectFromClipboard { get; }

        public ReactiveCommand<Unit, Unit> Send { get; }

        async void OnReceive()
        {
            var reader = receive.Reader;
            while (!cancel.IsCancellationRequested)
            {
                ReadResult result;
                try
                {
                    result = await reader.ReadAsync();
                }
                catch (InvalidOperationException)
                {
                    break;
                }
                if (result.IsCanceled || result.IsCompleted) return;
                if (!(result.Buffer.Length == 1 && result.Buffer.FirstSpan[0] == 1))
                {
                    using var aes = new AesGcm(key!, 16);
                    var iv = result.Buffer.Slice(0, 12).ToArray();
                    var bytes = result.Buffer.Slice(12 + 16);
                    var content = new byte[bytes.Length];
                    var tag = result.Buffer.Slice(12, 16).ToArray();
                    aes.Decrypt(iv, bytes.ToArray(), tag, content, iv);
                    var message = JsonSerializer.Deserialize<Message>(content);
                    Messages.Add(message!);
                }
                reader.AdvanceTo(result.Buffer.End);
            }
        }

        async Task OnSend(Message message)
        {
            message.Alignment = HorizontalAlignment.Right;
            if (stream is null) return;
            var iv = RandomBytes(12);
            var bytes = Encoding.UTF8.GetBytes(JsonSerializer.Serialize(message));
            var result = new byte[12 + 16 + bytes.Length];
            iv.CopyTo(result, 0);
            result[12] = (byte)'\n';
            using var aes = new AesGcm(key!, 16);
            aes.Encrypt(iv, bytes, result.AsSpan(12 + 16, bytes.Length), result.AsSpan(12, 16), iv);
            try
            {
                await stream.WriteAsync(result);
                if (stream.Socket.Connected) Messages.Add(message);
                else Disconnect();
            }
            catch (IOException)
            {
                Disconnect();
            }
        }

        async void Disconnect()
        {
            await cancel.CancelAsync();
            Connected = false;
            receive.Reader.CancelPendingRead();
            await receive.Reader.CompleteAsync();
            await receive.Writer.CompleteAsync();
            receive.Reset();
            stream?.Socket.Disconnect(false);
            stream?.Close();
            stream?.Dispose();
            stream = null;
        }

        async void GetPublicIP()
        {
            var res = await Client.GetStringAsync("https://service.mliybs.top/ip");

            foreach (var @interface in System.Net.NetworkInformation.NetworkInterface.GetAllNetworkInterfaces())
            {
                foreach (var ip in @interface.GetIPProperties().UnicastAddresses)
                {
                    if (ip.Address.ToString() == res)
                    {
                        PublicIP = res;
                        return;
                    }
                }
            }
        }

        async Task StartConnectAsync()
        {
            var pair = X25519.X25519KeyAgreement.GenerateKeyPair();
            var other = new byte[32];
            await stream!.WriteAsync(pair.PublicKey);
            await stream!.ReadAsync(other);
            key = X25519.X25519KeyAgreement.Agreement(pair.PrivateKey, other);
        }
    }

    public class Message(string content, HorizontalAlignment alignment = HorizontalAlignment.Left)
    {
        public string Content { get; } = content;

        [JsonIgnore]
        public HorizontalAlignment Alignment { get; set; } = alignment;
    }

    public record ConnectInfo(string IP, int Port);
}
