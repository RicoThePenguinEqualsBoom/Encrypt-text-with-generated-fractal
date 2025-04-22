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
        private void Form1_Load(object sender, EventArgs e)
        {
            comboBox1.SelectedItem = "Rainbow";
        }

        internal Form1()
        {
            this.Font = SystemFonts.MessageBoxFont;
            InitializeComponent();
        }

        #region Variable decleration
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

        private Bitmap inputBmp;
        private Bitmap outputBmp;

        private Complex keyC;

        private byte[] key;
        private byte[] iv;
        private byte[] encrypted;

        private int height;
        private int width;
        #endregion

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
            if (inputText.Text != null) { encrypt(); }
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

                (key, iv) = ProcessKey.Generate();

                encrypted = ProcessKey.EncryptWithAes(Encoding.UTF8.GetBytes(inText), key, iv);

                if (!ImageSize.IsBigEnough(width, height, encrypted.Length))
                {
                    throw new Exception("image not big");
                }

                keyC = ProcessKey.GenerateFractalModifier(encrypted);

                Bitmap bmp = ProcessJulia.GenerateJulia(keyC, width, height, comboBox1.Text);

                outputBmp = ProcessJulia.EmbedDataLSB(bmp, encrypted);

                keyS = ProcessKey.ComposeKeyString(key, iv, keyC);

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
            width = int.Parse(ImageW.Text);
            height = int.Parse(ImageH.Text);
            if (ImageSize.IsBigEnough(width, height, inputText.Text.Length) == false)
            {
                (width, height) = ImageSize.ValidSize(width, height, inputText.Text);
            }
        }

        private void ImageH_LostFocus(object sender, EventArgs e)
        {
            width = int.Parse(ImageW.Text);
            height = int.Parse(ImageH.Text);
            if (ImageSize.IsBigEnough(width, height, inputText.Text.Length) == false)
            {
                (width, height) = ImageSize.ValidSize(width, height, inputText.Text);
            }
        }

        private void ImageW_LostFocus(object sender, EventArgs e)
        {
            width = int.Parse(ImageW.Text);
            height = int.Parse(ImageH.Text);
            if (ImageSize.IsBigEnough(width, height, inputText.Text.Length) == false)
            {
                (width, height) = ImageSize.ValidSize(width, height, inputText.Text);
            }
        }
    }
}
