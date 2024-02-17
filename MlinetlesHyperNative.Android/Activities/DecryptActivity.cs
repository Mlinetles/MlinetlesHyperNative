
namespace MlinetlesHyperNative.Android;

[Activity(
    Label = "解密该文件",
    Theme = "@style/MyTheme.NoActionBar",
    Icon = "@drawable/icon",
    ConfigurationChanges = ConfigChanges.Orientation | ConfigChanges.ScreenSize | ConfigChanges.UiMode,
    Exported = true),
IntentFilter([Intent.ActionView], Categories = [Intent.CategoryDefault], DataMimeType = "*/*")]
public class DecryptActivity : AvaloniaMainActivity
{
    protected override void OnCreate(Bundle savedInstanceState)
    {
        base.OnCreate(savedInstanceState);

        this.Initialize();

        var stream = new FileInputStream(ContentResolver!.OpenFileDescriptor(Intent!.Data!, "r")!.FileDescriptor);

        var bytes = new byte[stream.Available()];

        var main = new MainView()
        {
            DataContext = new MainViewModel()
        };

        var decrypt = new Decrypt();

        var model = (DecryptViewModel)decrypt.DataContext!;

        model.SetFile(bytes);

        model.SetName(Intent!.DataString!.Split('/')[^1]);

        Task.Run(() =>
        {
            stream.Read(bytes);
            stream.Close();
            model.SetAvailable();
        });

        main.SetContent(decrypt);

        SetContentView(new AvaloniaView(this)
        {
            Content = main
        });
    }
}
