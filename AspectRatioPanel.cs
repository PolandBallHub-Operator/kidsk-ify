using System.Drawing.Drawing2D;

namespace KidsK_ify;

internal sealed class AspectRatioPanel : Panel
{
    private readonly PictureBox _pictureBox;
    private double _aspectRatio = 9d / 16d;

    public PictureBox Screen => _pictureBox;

    public AspectRatioPanel()
    {
        BackColor = Color.FromArgb(18, 24, 34);
        _pictureBox = new PictureBox
        {
            BackColor = Color.Black,
            SizeMode = PictureBoxSizeMode.StretchImage,
            BorderStyle = BorderStyle.FixedSingle,
            TabStop = true
        };
        Controls.Add(_pictureBox);
        Resize += (_, _) => FitScreen();
    }

    public void SetAspectRatio(int width, int height)
    {
        if (width <= 0 || height <= 0) return;
        _aspectRatio = width / (double)height;
        FitScreen();
    }

    private void FitScreen()
    {
        if (ClientSize.Width <= 0 || ClientSize.Height <= 0) return;
        var width = ClientSize.Width;
        var height = (int)Math.Round(width / _aspectRatio);
        if (height > ClientSize.Height)
        {
            height = ClientSize.Height;
            width = (int)Math.Round(height * _aspectRatio);
        }
        _pictureBox.Bounds = new Rectangle((ClientSize.Width - width) / 2,
            (ClientSize.Height - height) / 2, Math.Max(1, width), Math.Max(1, height));
    }
}
