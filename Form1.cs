using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.Windows.Forms;
using System.IO;

namespace SteganoTool
{
    internal partial class Form1 : Form
    {
        internal Form1()
        {
            this.Font = SystemFonts.MessageBoxFont;
            InitializeComponent();
        }

        private readonly OpenFileDialog ofd = new()
        {
            Filter = "Image files (*.png)|*.png",
            Title = "Veuiller choisir une image au format spécifier"
        };

        private readonly SaveFileDialog sfd = new()
        {
            Filter = "Image files (*.png)|*.png",
            Title = "Choisir où sauvegarder votre image",
            DefaultExt = "png",
            FileName = "output.png"
        };

        ImageFormat pngFormat = ImageFormat.Png;

        private string inText;
        private string outText;
        private string inKey;
        private string keyString;

        private Bitmap inputBmp;
        private Bitmap outputBmp;

        private ProcessKey outKey;

        private int height;
        private int width;

        private void csBtn_Click(object sender, EventArgs e)
        {
            if (outputKey.Text != null) { Clipboard.SetText(outputKey.Text); }
            if (sfd.ShowDialog() == DialogResult.OK)
            {
                outputBmp.Save(sfd.FileName, pngFormat);
                Application.Exit();
            }
        }

        private void inputBtn_Click(object sender, EventArgs e)
        {
            if (ofd.ShowDialog() == DialogResult.OK)
            {
                inputBmp = new Bitmap(ofd.FileName);
                inputImage.Image = inputBmp;
            }
        }

        private void pasteBtn_Click(object sender, EventArgs e)
        {
            if (Clipboard.GetText() != null) { encryptKey.Text = Clipboard.GetText(); }
        }

        private void copyBtn_Click(object sender, EventArgs e)
        {
            if (outputText.Text != null) { Clipboard.SetText(outputText.Text); }
        }

        private void encryptBtn_Click(object sender, EventArgs e)
        {
            if (inputText.Text != null)
            {
                encrypt();
            }
        }

        private void decryptBtn_Click(object sender, EventArgs e)
        {
            if (inputBmp != null) { decrypt(); }
        }

        private void encrypt()
        {
            try
            {
                /* inText = inputText.Text;

                 cryptL = trackBar1.Value;

                 int.TryParse(ImageH.Text, out height);
                 int.TryParse(ImageW.Text, out width); */

                inText = inputText.Text;
                width = int.Parse(ImageW.Text);
                height = int.Parse(ImageH.Text);

                outKey = ProcessKey.Generate(inText);
                keyString = outKey.ToString();

                outputBmp = ProcessJulia.GenerateJulia(width, height, outKey);

                outputKey.Text = keyString;
                outputImage.Image = outputBmp;
            }
            catch (Exception error)
            {
                MessageBox.Show(error.Message);
                return;
            }
        }

        private void decrypt()
        {
            try
            {
                inKey = encryptKey.Text;

                outText = ProcessJulia.DecryptFractal(inputBmp, inKey);

                outputText.Text = outText;
            }
            catch (Exception error)
            {
                MessageBox.Show(error.Message);
                return;
            }
        }

        private void inputText_TextChanged(object sender, EventArgs e)
        {
            (width, height)= ImageSize.CalculateMinimumSize(inputText.Text);
            ImageSize.IsValidSize(width, height, inputText.Text);
            ImageW.Text = width.ToString();
            ImageH.Text = height.ToString();
        }
    }
}
