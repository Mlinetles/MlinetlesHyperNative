using Avalonia.Controls;
using System.Threading.Tasks;
using System.Xml.Linq;

namespace MlinetlesHyperNative.ViewModels
{
    public class DecryptViewModel : ViewModelBase
    {
        public DecryptViewModel()
        {
            DecryptDownload = ReactiveCommand.CreateFromTask(async (Button button) => await Task.Run(() =>
            {
                Operate(button, CryptoOperate.Download);
            }), this.WhenAnyValue(x => x.Key, x => x.IsLoaded, (x, y) => y && IsKeyValid(x)));
            DecryptShare = ReactiveCommand.CreateFromTask(async (Button button) => await Task.Run(() =>
            {
                Operate(button, CryptoOperate.Share);
            }), this.WhenAnyValue(x => x.Key, x => x.IsLoaded, (x, y) => ShareImpl is not null && y && IsKeyValid(x)));
            DecryptOpen = ReactiveCommand.CreateFromTask(async (Button button) => await Task.Run(() =>
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

        public ReactiveCommand<Button, Unit> DecryptDownload { get; }

        public ReactiveCommand<Button, Unit> DecryptShare { get; }

        public ReactiveCommand<Button, Unit> DecryptOpen { get; }

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
            if (file[0].Name.Length > 4 && file[0].Name.EndsWith(".mnd")) fileName = file[0].Name[..^4];
            using var stream = await file[0].OpenReadAsync();
            selectedFile = new byte[stream.Length];
            await stream.ReadAsync(selectedFile);
            IsLoaded = true;
        }

        void Operate(Button button, CryptoOperate operate)
        {
            if (selectedFile is null) return;
            Span<byte> magic = Encoding.UTF8.GetBytes("MND UTF-8\n");
            for (var i = 0; i < magic.Length; i++) if (selectedFile[i] != magic[i]) return;
            var start = magic.Length;
            Dictionary<string, string> dic = new(2);
            while (true)
            {
                var index = Array.FindIndex(selectedFile, start, x => x == "\n"u8[0]);
                if (index == -1) break;
                var span = selectedFile.AsSpan(start, index == start ? 1 : index - start);
                start = index + 1;
                if (span[0] == "\n"u8[0]) break;
                var pair = Encoding.UTF8.GetString(span).Split(':');
                if (pair.Length == 1) continue;
                dic.Add(pair[0], pair[1]);
            }
            if (!dic.TryGetValue("IV", out _))
            {
                return;
            }
            Span<byte> key = FromKeyToBytes(Key, stackalloc byte[32]);
            Span<byte> iv = stackalloc byte[12];
            if (!Convert.TryFromBase64String(dic["IV"], iv, out var _length) || _length != 12)
            {
                return;
            }
            if (selectedFile is null) return;
            using var aes = new AesGcm(FromKeyToBytes(_key, stackalloc byte[33]), 16);
            Span<byte> tag = selectedFile.AsSpan(^16..);
            var result = new byte[selectedFile.Length - start - 16];
            var file = selectedFile.AsSpan(start..^16);
            try
            {
                aes.Decrypt(iv, file, tag, result, selectedFile.AsSpan(..start));
            }
            catch (CryptographicException e)
            {
                Message = $"解密失败！错误信息：{e.Message}";
                return;
            }

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

        public void SetFile(byte[] bytes) => selectedFile = bytes;

        public void SetAvailable() => IsLoaded = true;

        public void SetName(string name) => fileName = name.Length > 4 && name.EndsWith(".mnd") ? name[..^4] : name;
    }
}
