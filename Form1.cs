using System.Drawing.Imaging;
using System.Numerics;
using System.Text;

namespace SteganoTool
{
    internal partial class Form1 : Form
    {
        private void Form1_Load(object sender, EventArgs e)
        {
            gBox.SelectedItem = "Classic";
            fBox.SelectedItem = "Julia";
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

        readonly ImageFormat pngFormat = ImageFormat.Png;

        private string EText;
        private string DText;
        private string key;
        private string DiOrCo = "D";

        private Bitmap EBmp;
        private Bitmap DBmp;

        private Complex keyC;

        private byte[] EKey;
        private byte[] EIv;
        private byte[] encrypted;
        private byte[] DKey;
        private byte[] DIv;
        private byte[] decrypted;

        private int height;
        private int width;

        private double EscapeRadius = 2.0;
        private double DReal;
        private double DImag;
        #endregion

        private void csBtn_Click(object sender, EventArgs e)
        {
            if (outputKey.Text != null) { Clipboard.SetText(outputKey.Text); }
            if (sfd.ShowDialog() == DialogResult.OK)
            {
                EBmp.Save(sfd.FileName, pngFormat);
                Application.Exit();
            }
        }

        private void inputBtn_Click(object sender, EventArgs e)
        {
            if (ofd.ShowDialog() == DialogResult.OK)
            {
                DBmp = new Bitmap(ofd.FileName);
                inputImage.Image = DBmp;
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
            if (DBmp != null) { decrypt(); }
        }

        private void encrypt()
        {
            try
            {
                EText = inputText.Text;
                height = int.Parse(ImageH.Text);
                width = int.Parse(ImageW.Text);

                (EKey, EIv) = ProcessKey.Generate();

                encrypted = ProcessKey.EncryptWithAes(Encoding.UTF8.GetBytes(EText), EKey, EIv);

                keyC = ProcessKey.GenerateFractalModifier(encrypted);

                while (ProcessFractal.CheckIterations(keyC, width, height, EscapeRadius) == false)
                {
                    MessageBox.Show("La clé est incorrecte ou l'image ne contient pas de message caché.");
                    (EKey, EIv) = ProcessKey.Generate();
                    encrypted = ProcessKey.EncryptWithAes(Encoding.UTF8.GetBytes(EText), EKey, EIv);
                    keyC = ProcessKey.GenerateFractalModifier(encrypted);
                }

                Bitmap bmp = ProcessFractal.GenerateFractal(keyC, width, height, EscapeRadius, gBox.Text, fBox.Text, DiOrCo);

                EBmp = ProcessFractal.EmbedLSB(bmp, encrypted);

                key = ProcessKey.ComposeKeyString(EKey, EIv, keyC);

                outputImage.Image = EBmp;
                outputKey.Text = key;
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
                key = encryptKey.Text;

                (DKey, DIv, DReal, DImag) = ProcessKey.ParseKeyString(key);

                decrypted = ProcessFractal.ExtractLSB(DBmp);

                DText = Encoding.UTF8.GetString(ProcessKey.DecryptWithAes(decrypted, DKey, DIv));

                if (DKey == null || DIv == null || DReal == 0 || DImag == 0 || decrypted == null || DText == null)
                {
                    MessageBox.Show("La clé est incorrecte ou l'image ne contient pas de message caché.");
                    return;
                }

                outputText.Text = DText;
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
            ImageW.Text = width.ToString();
            ImageH.Text = height.ToString();
        }

        private void ImageH_LostFocus(object sender, EventArgs e)
        {
            width = int.Parse(ImageW.Text);
            height = int.Parse(ImageH.Text);
            if (ImageSize.IsBigEnough(width, height, inputText.Text.Length) == false)
            {
                (width, height) = ImageSize.ValidSize(width, height, inputText.Text);
            }
            ImageW.Text = width.ToString();
            ImageH.Text = height.ToString();
        }

        private void ImageW_LostFocus(object sender, EventArgs e)
        {
            width = int.Parse(ImageW.Text);
            height = int.Parse(ImageH.Text);
            if (ImageSize.IsBigEnough(width, height, inputText.Text.Length) == false)
            {
                (width, height) = ImageSize.ValidSize(width, height, inputText.Text);
            }
            ImageW.Text = width.ToString();
            ImageH.Text = height.ToString();
        }

        private void tabControl1_SelectedIndexChanged(object sender, EventArgs e)
        {
            if (tabControl1.SelectedTab == tabControl1.TabPages[1])
            {
                MessageBox.Show("Warning!!! Wrong input of decryption parameters will result in BSOD");
            }
        }

        private void Circle_CheckedChanged(object sender, EventArgs e)
        {
            if (Circle.Checked == true)
            {
                EscapeRadius = 1.0;
            }
            else
            {
                EscapeRadius = 2.0;
            }
        }

        private void fBox_SelectedIndexChanged(object sender, EventArgs e)
        {
            if (fBox.SelectedIndex > 0)
            {
                DiOrCo = "C";
                gBox.Items.Clear();
                gBox.Items.Add("RGB");
                gBox.SelectedItem = "RGB";
                gBox.Items.Add("CYM");
                noCircle.Enabled = false;
                Circle.Enabled = false;
            }
            else
            {
                gBox.Items.Remove("RGB");
            }
        }
    }
}
