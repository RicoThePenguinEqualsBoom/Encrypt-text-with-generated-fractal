using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.Windows.Forms;
using System.IO;
using System.Numerics;
using System.Security.Cryptography;
using System.Text;

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
        private string keyS;
        private string encryptedText;

        private Bitmap inputBmp;
        private Bitmap outputBmp;

        private Complex keyC;

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
                inText = inputText.Text;
                height = int.Parse(ImageH.Text);
                width = int.Parse(ImageW.Text);

                var (key, iv) = ProcessKey.Generate();

                var encrypted = ProcessKey.EncryptWithAes(Encoding.UTF8.GetBytes(inText), key, iv);

                if (!ImageSize.IsImageLargeEnough(width, height, encrypted.Length))
                {
                    throw new Exception("image not big");
                }

                var (real, imag) = ProcessKey.GenerateFractalModifier(encrypted);

                Bitmap bmp = ProcessJulia.GenerateJulia(real, imag, width, height);

                outputBmp = ProcessJulia.EmbedDataLSB(bmp, encrypted);

                keyS = ProcessKey.ComposeKeyString(key, iv, real, imag);

                var (dkey, div, dreal, dimag) = ProcessKey.ParseKeyString(keyS);

                var decrypted = ProcessJulia.ExtractDataLSB(outputBmp);

                MessageBox.Show($"result: {Encoding.UTF8.GetString(ProcessKey.DecryptWithAes(decrypted, dkey, div))}");

                outputImage.Image = outputBmp;
                outputKey.Text = keyS;
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message);
            }
        }

        private void decrypt()
        {
            try
            {
                keyS = encryptKey.Text;

                var (DKey, DIv, DReal, DImag) = ProcessKey.ParseKeyString(keyS);

                var decrypted = ProcessJulia.ExtractDataLSB(inputBmp);

                outText = Encoding.UTF8.GetString(ProcessKey.DecryptWithAes(decrypted, DKey, DIv));

                if (outText == null)
                {
                    MessageBox.Show("La clé est incorrecte ou l'image ne contient pas de message caché.");
                    return;
                }

                outputText.Text = outText;
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message);
            }
        }

        private void inputText_TextChanged(object sender, EventArgs e)
        {

        }

        private void ImageH_LostFocus(object sender, EventArgs e)
        {

        }

        private void ImageW_LostFocus(object sender, EventArgs e)
        {

        }
    }
}
