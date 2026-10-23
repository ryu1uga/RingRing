using System.IO;
using System.Windows;
using System.Windows.Media.Imaging;
using QRCoder;
using RingRing.Server;

namespace RingRing.Views;

public partial class QrWindow : Window
{
    public QrWindow(int port)
    {
        InitializeComponent();

        var ips = NetworkInfo.GetLanAddresses();
        if (ips.Count == 0)
        {
            UrlText.Text = "Sin conexión de red";
            OtherUrls.Text = "Conecta la PC a una red Wi-Fi o Ethernet.";
            CopyButton.IsEnabled = false;
            return;
        }

        var url = $"http://{ips[0]}:{port}";
        UrlText.Text = url;
        QrImage.Source = RenderQr(url);

        if (ips.Count > 1)
            OtherUrls.Text = "También: " + string.Join("  ·  ", ips.Skip(1).Select(ip => $"http://{ip}:{port}"));

        CopyButton.Click += (_, _) =>
        {
            try { Clipboard.SetText(url); CopyButton.Content = "Copiado"; }
            catch { /* portapapeles ocupado */ }
        };
    }

    private static BitmapImage RenderQr(string text)
    {
        using var generator = new QRCodeGenerator();
        using var data = generator.CreateQrCode(text, QRCodeGenerator.ECCLevel.M);
        var png = new PngByteQRCode(data).GetGraphic(20, [0x15, 0x18, 0x1D], [0xFF, 0xFF, 0xFF]);

        var image = new BitmapImage();
        using var ms = new MemoryStream(png);
        image.BeginInit();
        image.CacheOption = BitmapCacheOption.OnLoad;
        image.StreamSource = ms;
        image.EndInit();
        image.Freeze();
        return image;
    }
}
