using System.Diagnostics;
using System.Drawing.Imaging;
using System.Numerics;
using System.Security.Cryptography;
using System.Text;

namespace SteganoTool
{
    internal partial class Form1 : Form
    {
        //Be sure to load the initial combobox selections
        private void Form1_Load(object sender, EventArgs e)
        {
            gBox.SelectedItem = "Classic";
            fBox.SelectedItem = "Julia";
        }

        //Be sure to load the form with the correct font
        internal Form1()
        {
            this.Font = SystemFonts.MessageBoxFont;
            InitializeComponent();
        }

        //Declare all your variables
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
        string gboxText;
        string fboxText;

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

        //Make sure you aren't trying to save a null image/copy null text
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

        //Make it an async to complement the called method
        private async void encryptBtn_Click(object sender, EventArgs e)
        {
            if (inputText.Text != null) { await encrypt(); }
        }

        private void decryptBtn_Click(object sender, EventArgs e)
        {
            if (DBmp != null) { decrypt(); }
        }

        //Make encryption/generation an async task to ensure no application "freezing"
        private async Task encrypt()
        {
            try
            {
                //Set the necessary variables to the input values
                EText = inputText.Text;
                height = int.Parse(ImageH.Text);
                width = int.Parse(ImageW.Text);
                gboxText = gBox.Text;
                fboxText = fBox.Text;

                //Use the necessary methods for the other variables
                (EKey, EIv) = ProcessKey.Generate();

                encrypted = ProcessKey.EncryptWithAes(Encoding.UTF8.GetBytes(EText), EKey, EIv);

                keyC = ProcessKey.GenerateFractalModifier(encrypted);

                //Check if keyC will generate a valid fractal (not void and doesn't take too long)
                while (ProcessFractal.CheckIterations(keyC, width, height, EscapeRadius, fboxText) == false)
                {
                    (EKey, EIv) = ProcessKey.Generate();
                    encrypted = ProcessKey.EncryptWithAes(Encoding.UTF8.GetBytes(EText), EKey, EIv);
                    keyC = ProcessKey.GenerateFractalModifier(encrypted);
                }

                //Generate the valid fractal with an await task run to prevent freezing
                Bitmap bmp = await Task.Run(() =>
                    ProcessFractal.GenerateFractal(keyC, width, height, EscapeRadius, gboxText, fboxText, DiOrCo)
                );

                //Embed the encrypted text into the fractal
                EBmp = ProcessFractal.EmbedLSB(bmp, encrypted);

                //Parse the key components into a continuous string
                key = ProcessKey.ComposeKeyString(EKey, EIv, keyC);

                //Display the final image and key string
                outputImage.Image = EBmp;
                outputKey.Text = key;
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message);
            }
        }

        //No need to make decryption async as it doesn't take as many resources to perform (even on large messages)
        private void decrypt()
        {
            try
            {
                key = encryptKey.Text;

                //Parse the key string into its components
                (DKey, DIv, DReal, DImag) = ProcessKey.ParseKeyString(key);

                //Decrypt the image using the same method as encryption
                decrypted = ProcessFractal.ExtractLSB(DBmp);

                //Decrypt the text using the same method as encryption
                DText = Encoding.UTF8.GetString(ProcessKey.DecryptWithAes(decrypted, DKey, DIv));

                //Check if the decryption was successful
                if (DKey == null || DIv == null || DReal == 0 || DImag == 0 || decrypted == null ||
                    string.IsNullOrWhiteSpace(DText) || string.IsNullOrEmpty(DText))
                {
                    Application.Exit();
                    return;
                }

                outputText.Text = DText;
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message);
            }
        }

        //Whenever the input text changes or the image size is changed, make sure the image is big enough
        private void inputText_TextChanged(object sender, EventArgs e)
        {
            width = int.Parse(ImageW.Text);
            height = int.Parse(ImageH.Text);
            if (!ImageSize.IsBigEnough(width, height, inputText.Text.Length))
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
            if (!ImageSize.IsBigEnough(width, height, inputText.Text.Length))
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
            if (!ImageSize.IsBigEnough(width, height, inputText.Text.Length))
            {
                (width, height) = ImageSize.ValidSize(width, height, inputText.Text);
            }
            ImageW.Text = width.ToString();
            ImageH.Text = height.ToString();
        }

        //Aesthetic option
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

        //Leftover from trying to implement other fractal types 
        /*private void fBox_SelectedIndexChanged(object sender, EventArgs e)
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
        }*/

        //Reload the image in the newly selected color without changing the fractal (promotes experimentation)
        private async void gBox_SelectedIndexChanged(object sender, EventArgs e)
        {
            if (key != null)
            {
                height = int.Parse(ImageH.Text);
                width = int.Parse(ImageW.Text);
                gboxText = gBox.Text;
                fboxText = fBox.Text;

                Bitmap bmp = await Task.Run(() => 
                    ProcessFractal.GenerateFractal(keyC, width, height, EscapeRadius, gboxText, fboxText, DiOrCo)
                );

                EBmp = ProcessFractal.EmbedLSB(bmp, encrypted);

                outputImage.Image = EBmp;
            }
        }
    }
}
