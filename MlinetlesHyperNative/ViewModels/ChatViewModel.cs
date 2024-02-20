namespace MlinetlesHyperNative.ViewModels
{
    public class ChatViewModel : ViewModelBase
    {
        private byte[]? key = null;

        private string? publicIP = null;

        private readonly Pipe receive = new();

        private Socket? client = null;

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

                var socket = new Socket(SocketType.Stream, ProtocolType.Tcp);

                try
                {
                    await socket.ConnectAsync(info.IP, info.Port);
                }
                catch (SocketException)
                {
                    return;
                }

                Connected = true;

                client = socket;

                await StartConnectAsync();

                while (!cancel.IsCancellationRequested && client.Connected)
                {
                    try
                    {
                        var length = await socket.ReceiveAsync(receive.Writer.GetMemory());

                        if (length == 0)
                        {
                            await socket.SendAsync(new byte[] { 1 });
                            if (!client.Connected) break;
                        }

                        receive.Writer.Advance(length);

                        await receive.Writer.FlushAsync();
                    }
                    catch (SocketException e) when (e.NativeErrorCode != 10035)
                    {
                        break;
                    }
                }

                Disconnect();
            });

            WaitForConnect = ReactiveCommand.CreateFromTask(async (Button button) =>
            {
                var clipboard = TopLevel.GetTopLevel(button)?.Clipboard ?? throw new NullReferenceException();

                using var listener = new Socket(SocketType.Stream, ProtocolType.Tcp);

                listener.Bind(new IPEndPoint(IPAddress.IPv6Any, 0));

                listener.Listen();

                await clipboard.SetTextAsync(JsonSerializer.Serialize<ConnectInfo>(new(PublicIP!, ((IPEndPoint)listener.LocalEndPoint!).Port)));

                var socket = await listener.AcceptAsync();

                listener.Close();

                client = socket;

                Connected = true;

                await StartConnectAsync();

                while (!cancel.IsCancellationRequested && client.Connected)
                {
                    try
                    {
                        var length = await client.ReceiveAsync(receive.Writer.GetMemory());

                        if (length == 0)
                        {
                            await client.SendAsync(new byte[] { 1 });
                            if (!client.Connected) break;
                        }

                        if (cancel.IsCancellationRequested) break;

                        receive.Writer.Advance(length);

                        await receive.Writer.FlushAsync();
                    }
                    catch (SocketException e) when (e.NativeErrorCode != 10035)
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
            if (client is null) return;
            var iv = RandomBytes(12);
            var bytes = Encoding.UTF8.GetBytes(JsonSerializer.Serialize(message));
            var result = new byte[12 + 16 + bytes.Length];
            iv.CopyTo(result, 0);
            result[12] = (byte)'\n';
            using var aes = new AesGcm(key!, 16);
            aes.Encrypt(iv, bytes, result.AsSpan(12 + 16, bytes.Length), result.AsSpan(12, 16), iv);
            try
            {
                await client.SendAsync(result);
                if (client.Connected) Messages.Add(message);
                else Disconnect();
            }
            catch (SocketException)
            {
                Disconnect();
            }
        }

        async void Disconnect()
        {
            ToastImpl?.Invoke("连接已断开！");
            await cancel.CancelAsync();
            Connected = false;
            receive.Reader.CancelPendingRead();
            await receive.Reader.CompleteAsync();
            await receive.Writer.CompleteAsync();
            receive.Reset();
            client?.Disconnect(false);
            client?.Close();
            client?.Dispose();
            client = null;
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
            await client!.SendAsync(pair.PublicKey);
            await client!.ReceiveAsync(other);
            key = X25519.X25519KeyAgreement.Agreement(pair.PrivateKey, other);
            ToastImpl?.Invoke("已连接！");
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
