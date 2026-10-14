namespace KidsK_ify;

internal sealed class SplashForm : Form
{
    public SplashForm()
    {
        FormBorderStyle = FormBorderStyle.None;
        StartPosition = FormStartPosition.CenterScreen;
        ClientSize = new Size(380, 410);
        BackColor = Color.FromArgb(15, 23, 42);
        ShowInTaskbar = false;
        DoubleBuffered = true;

        var iconPath = Path.Combine(AppContext.BaseDirectory, "Assets", "appicon.png");
        if (File.Exists(iconPath))
        {
            var logo = new PictureBox
            {
                Image = Image.FromFile(iconPath),
                SizeMode = PictureBoxSizeMode.Zoom,
                BackColor = Color.Transparent,
                Bounds = new Rectangle(102, 34, 176, 250)
            };
            Controls.Add(logo);
        }

        Controls.Add(new Label
        {
            Text = "KidsK-ify",
            ForeColor = Color.White,
            Font = new Font("Segoe UI", 25, FontStyle.Bold),
            TextAlign = ContentAlignment.MiddleCenter,
            Bounds = new Rectangle(20, 294, 340, 48)
        });
        Controls.Add(new Label
        {
            Text = ".NET 8  ·  Android Remote Screen",
            ForeColor = Color.FromArgb(148, 163, 184),
            Font = new Font("Segoe UI", 10),
            TextAlign = ContentAlignment.MiddleCenter,
            Bounds = new Rectangle(20, 342, 340, 28)
        });
    }
}
