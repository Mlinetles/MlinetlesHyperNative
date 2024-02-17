using Avalonia.Controls;

namespace MlinetlesHyperNative.ViewModels
{
    public class EncryptViewModel : ViewModelBase
    {
        public EncryptViewModel()
        {
            EncryptDownload = ReactiveCommand.CreateFromTask(async (Button button) => await Task.Run(() =>
            {
                Operate(button, CryptoOperate.Download);
            }), this.WhenAnyValue(x => x.Key, x => x.IsLoaded, (x, y) => y && IsKeyValid(x)));
            EncryptShare = ReactiveCommand.CreateFromTask(async (Button button) => await Task.Run(() =>
            {
                Operate(button, CryptoOperate.Share);
            }), this.WhenAnyValue(x => x.Key, x => x.IsLoaded, (x, y) => ShareImpl is not null && y && IsKeyValid(x)));
            EncryptOpen = ReactiveCommand.CreateFromTask(async (Button button) => await Task.Run(() =>
            {
                Operate(button, CryptoOperate.Open);
            }), this.WhenAnyValue(x => x.Key, x => x.IsLoaded, (x, y) => OpenImpl is not null && y && IsKeyValid(x)));
        }

        private string? _key = null;

        private bool isLoaded = false;

        private byte[]? selectedFile = null;

        private string? fileName = null;

        private string? message = null;

        public string? Key { get => _key; set => this.RaiseAndSetIfChanged(ref _key, value); }

        public bool IsLoaded { get => isLoaded; set => this.RaiseAndSetIfChanged(ref isLoaded, value); }

        public string? Message { get => message; set => this.RaiseAndSetIfChanged(ref message, value); }

        public ReactiveCommand<Button, Unit> EncryptDownload { get; }

        public ReactiveCommand<Button, Unit> EncryptShare { get; }

        public ReactiveCommand<Button, Unit> EncryptOpen { get; }

        public async void SelectFile(Button button)
        {
            var toplevel = TopLevel.GetTopLevel(button);
            if (toplevel is null || !toplevel.StorageProvider.CanOpen) return;
            var file = await toplevel.StorageProvider.OpenFilePickerAsync(new()
            {
                Title = "选择文件",
                AllowMultiple = false
            });
            if (file is null || file.Count == 0) return;
            fileName = file[0].Name + ".mnd";
            using var stream = await file[0].OpenReadAsync();
            selectedFile = new byte[stream.Length];
            await stream.ReadAsync(selectedFile);
            IsLoaded = true;
        }

        void Operate(Button button, CryptoOperate operate)
        {
            var iv = RandomBytes(12);
            var builder = new StringBuilder();
            builder.Append("MND UTF-8\n");
            builder.Append($"IV:{Convert.ToBase64String(iv)}\n");
            builder.Append('\n');
            var header = Encoding.UTF8.GetBytes(builder.ToString());
            if (selectedFile is null) return;
            using var aes = new AesGcm(FromKeyToBytes(_key, stackalloc byte[33]), 16);
            Span<byte> tag = stackalloc byte[16];
            var result = new byte[header.Length + selectedFile.Length + 16];
            try
            {
                aes.Encrypt(iv, selectedFile, result.AsSpan(header.Length, selectedFile.Length), tag, header);
            }
            catch (CryptographicException e)
            {
                Message = $"加密失败！错误信息：{e.Message}";
                return;
            }
            header.CopyTo(result, 0);
            tag.CopyTo(result.AsSpan(header.Length + selectedFile.Length));
            if (operate == CryptoOperate.Download) Download(result, button);
            else if (operate == CryptoOperate.Share) ShareImpl?.Invoke(fileName!, result);
            else if (operate == CryptoOperate.Open) OpenImpl?.Invoke(fileName!, result);
        }

        async void Download(byte[] bytes, Button button)
        {
            var file = await TopLevel.GetTopLevel(button)!.StorageProvider!.SaveFilePickerAsync(new()
            {
                SuggestedFileName = fileName
            });
            if (file is null) return;
            using var stream = await file.OpenWriteAsync();
            await stream.WriteAsync(bytes);
        }

        public void RandomKey()
        {
            Key = RandomString(stackalloc byte[32]);
        }

        public void SetFile(byte[] bytes) => selectedFile = bytes;

        public void SetAvailable() => IsLoaded = true;

        public void SetName(string name) => fileName = name + ".mnd";
    }
}
