using System;
using System.Drawing;
using System.Windows.Forms;

namespace SteganoTool
{
    partial class Form1
    {
        /// <summary>
        ///  Required designer variable.
        /// </summary>
        private System.ComponentModel.IContainer components = null;

        /// <summary>
        ///  Clean up any resources being used.
        /// </summary>
        /// <param name="disposing">true if managed resources should be disposed; otherwise, false.</param>
        protected override void Dispose(bool disposing)
        {
            if (disposing && (components != null))
            {
                components.Dispose();
            }
            base.Dispose(disposing);
        }

        #region Windows Form Designer generated code

        /// <summary>
        ///  Required method for Designer support - do not modify
        ///  the contents of this method with the code editor.
        /// </summary>
        private void InitializeComponent()
        {
            tabControl1 = new TabControl();
            tabPage1 = new TabPage();
            label11 = new Label();
            fBox = new ComboBox();
            Circle = new RadioButton();
            noCircle = new RadioButton();
            label2 = new Label();
            gBox = new ComboBox();
            label10 = new Label();
            label9 = new Label();
            label8 = new Label();
            label7 = new Label();
            ImageW = new TextBox();
            ImageH = new TextBox();
            label3 = new Label();
            label1 = new Label();
            csBtn = new Button();
            encryptBtn = new Button();
            outputKey = new TextBox();
            outputImage = new PictureBox();
            inputText = new RichTextBox();
            tabPage2 = new TabPage();
            label6 = new Label();
            label5 = new Label();
            label4 = new Label();
            pasteBtn = new Button();
            encryptKey = new RichTextBox();
            copyBtn = new Button();
            decryptBtn = new Button();
            inputBtn = new Button();
            outputText = new RichTextBox();
            inputImage = new PictureBox();
            tabControl1.SuspendLayout();
            tabPage1.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)outputImage).BeginInit();
            tabPage2.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)inputImage).BeginInit();
            SuspendLayout();
            // 
            // tabControl1
            // 
            tabControl1.Controls.Add(tabPage1);
            tabControl1.Controls.Add(tabPage2);
            tabControl1.Location = new Point(12, 12);
            tabControl1.Name = "tabControl1";
            tabControl1.SelectedIndex = 0;
            tabControl1.Size = new Size(1322, 634);
            tabControl1.TabIndex = 20;
            tabControl1.SelectedIndexChanged += tabControl1_SelectedIndexChanged;
            // 
            // tabPage1
            // 
            tabPage1.BackColor = SystemColors.Control;
            tabPage1.Controls.Add(label11);
            tabPage1.Controls.Add(fBox);
            tabPage1.Controls.Add(Circle);
            tabPage1.Controls.Add(noCircle);
            tabPage1.Controls.Add(label2);
            tabPage1.Controls.Add(gBox);
            tabPage1.Controls.Add(label10);
            tabPage1.Controls.Add(label9);
            tabPage1.Controls.Add(label8);
            tabPage1.Controls.Add(label7);
            tabPage1.Controls.Add(ImageW);
            tabPage1.Controls.Add(ImageH);
            tabPage1.Controls.Add(label3);
            tabPage1.Controls.Add(label1);
            tabPage1.Controls.Add(csBtn);
            tabPage1.Controls.Add(encryptBtn);
            tabPage1.Controls.Add(outputKey);
            tabPage1.Controls.Add(outputImage);
            tabPage1.Controls.Add(inputText);
            tabPage1.Location = new Point(4, 24);
            tabPage1.Name = "tabPage1";
            tabPage1.Padding = new Padding(3);
            tabPage1.Size = new Size(1314, 606);
            tabPage1.TabIndex = 0;
            tabPage1.Text = "Encrypt";
            // 
            // label11
            // 
            label11.AutoSize = true;
            label11.Location = new Point(437, 249);
            label11.Name = "label11";
            label11.Size = new Size(68, 15);
            label11.TabIndex = 21;
            label11.Text = "Fractal type";
            // 
            // fBox
            // 
            fBox.FormattingEnabled = true;
            fBox.Items.AddRange(new object[] { "Julia", "Newton", "Nova" });
            fBox.Location = new Point(437, 267);
            fBox.Name = "fBox";
            fBox.Size = new Size(135, 23);
            fBox.TabIndex = 20;
            fBox.SelectedIndexChanged += fBox_SelectedIndexChanged;
            // 
            // Circle
            // 
            Circle.AutoSize = true;
            Circle.Location = new Point(722, 186);
            Circle.Name = "Circle";
            Circle.Size = new Size(55, 19);
            Circle.TabIndex = 19;
            Circle.Text = "Circle";
            Circle.UseVisualStyleBackColor = true;
            Circle.CheckedChanged += Circle_CheckedChanged;
            // 
            // noCircle
            // 
            noCircle.AutoSize = true;
            noCircle.Checked = true;
            noCircle.Location = new Point(722, 161);
            noCircle.Name = "noCircle";
            noCircle.Size = new Size(72, 19);
            noCircle.TabIndex = 18;
            noCircle.TabStop = true;
            noCircle.Text = "No circle";
            noCircle.UseVisualStyleBackColor = true;
            // 
            // label2
            // 
            label2.AutoSize = true;
            label2.Location = new Point(722, 249);
            label2.Name = "label2";
            label2.Size = new Size(89, 15);
            label2.TabIndex = 17;
            label2.Text = "Gradient theme";
            // 
            // gBox
            // 
            gBox.FormattingEnabled = true;
            gBox.Items.AddRange(new object[] { "Classic", "Aurora", "Rainbow" });
            gBox.Location = new Point(722, 267);
            gBox.Name = "gBox";
            gBox.Size = new Size(121, 23);
            gBox.TabIndex = 16;
            // 
            // label10
            // 
            label10.AutoSize = true;
            label10.Location = new Point(426, 126);
            label10.Name = "label10";
            label10.Size = new Size(43, 15);
            label10.TabIndex = 15;
            label10.Text = "Height";
            // 
            // label9
            // 
            label9.AutoSize = true;
            label9.Location = new Point(570, 126);
            label9.Name = "label9";
            label9.Size = new Size(39, 15);
            label9.TabIndex = 14;
            label9.Text = "Width";
            // 
            // label8
            // 
            label8.AutoSize = true;
            label8.Location = new Point(513, 147);
            label8.Name = "label8";
            label8.Size = new Size(14, 15);
            label8.TabIndex = 13;
            label8.Text = "X";
            // 
            // label7
            // 
            label7.AutoSize = true;
            label7.Location = new Point(484, 84);
            label7.Name = "label7";
            label7.Size = new Size(62, 15);
            label7.TabIndex = 12;
            label7.Text = "Image size";
            // 
            // ImageW
            // 
            ImageW.Location = new Point(542, 144);
            ImageW.Name = "ImageW";
            ImageW.Size = new Size(100, 23);
            ImageW.TabIndex = 11;
            ImageW.Text = "2000";
            ImageW.LostFocus += ImageW_LostFocus;
            // 
            // ImageH
            // 
            ImageH.Location = new Point(398, 144);
            ImageH.Name = "ImageH";
            ImageH.Size = new Size(100, 23);
            ImageH.TabIndex = 10;
            ImageH.Text = "2000";
            ImageH.LostFocus += ImageH_LostFocus;
            // 
            // label3
            // 
            label3.AutoSize = true;
            label3.Location = new Point(940, 24);
            label3.Name = "label3";
            label3.Size = new Size(201, 15);
            label3.TabIndex = 9;
            label3.Text = "Generated image and encryption key";
            // 
            // label1
            // 
            label1.AutoSize = true;
            label1.Location = new Point(24, 24);
            label1.Name = "label1";
            label1.Size = new Size(110, 15);
            label1.TabIndex = 7;
            label1.Text = "Message to encrypt";
            // 
            // csBtn
            // 
            csBtn.Location = new Point(1190, 407);
            csBtn.Name = "csBtn";
            csBtn.Size = new Size(95, 28);
            csBtn.TabIndex = 6;
            csBtn.Text = "Copy and Save";
            csBtn.UseVisualStyleBackColor = true;
            csBtn.Click += csBtn_Click;
            // 
            // encryptBtn
            // 
            encryptBtn.Location = new Point(581, 429);
            encryptBtn.Name = "encryptBtn";
            encryptBtn.Size = new Size(148, 51);
            encryptBtn.TabIndex = 5;
            encryptBtn.Text = "Encrypt";
            encryptBtn.UseVisualStyleBackColor = true;
            encryptBtn.Click += encryptBtn_Click;
            // 
            // outputKey
            // 
            outputKey.Location = new Point(940, 407);
            outputKey.Name = "outputKey";
            outputKey.Size = new Size(168, 23);
            outputKey.TabIndex = 4;
            // 
            // outputImage
            // 
            outputImage.BorderStyle = BorderStyle.FixedSingle;
            outputImage.Location = new Point(940, 56);
            outputImage.Name = "outputImage";
            outputImage.Size = new Size(345, 345);
            outputImage.SizeMode = PictureBoxSizeMode.Zoom;
            outputImage.TabIndex = 1;
            outputImage.TabStop = false;
            // 
            // inputText
            // 
            inputText.Location = new Point(24, 56);
            inputText.Name = "inputText";
            inputText.Size = new Size(345, 345);
            inputText.TabIndex = 0;
            inputText.Text = "";
            inputText.TextChanged += inputText_TextChanged;
            // 
            // tabPage2
            // 
            tabPage2.BackColor = SystemColors.Control;
            tabPage2.Controls.Add(label6);
            tabPage2.Controls.Add(label5);
            tabPage2.Controls.Add(label4);
            tabPage2.Controls.Add(pasteBtn);
            tabPage2.Controls.Add(encryptKey);
            tabPage2.Controls.Add(copyBtn);
            tabPage2.Controls.Add(decryptBtn);
            tabPage2.Controls.Add(inputBtn);
            tabPage2.Controls.Add(outputText);
            tabPage2.Controls.Add(inputImage);
            tabPage2.Location = new Point(4, 24);
            tabPage2.Name = "tabPage2";
            tabPage2.Padding = new Padding(3);
            tabPage2.Size = new Size(1314, 606);
            tabPage2.TabIndex = 1;
            tabPage2.Text = "Decrypt";
            // 
            // label6
            // 
            label6.AutoSize = true;
            label6.Location = new Point(940, 24);
            label6.Name = "label6";
            label6.Size = new Size(110, 15);
            label6.TabIndex = 9;
            label6.Text = "Decrypted message";
            // 
            // label5
            // 
            label5.AutoSize = true;
            label5.Location = new Point(581, 133);
            label5.Name = "label5";
            label5.Size = new Size(85, 15);
            label5.TabIndex = 8;
            label5.Text = "Encryption key";
            // 
            // label4
            // 
            label4.AutoSize = true;
            label4.Location = new Point(24, 24);
            label4.Name = "label4";
            label4.Size = new Size(97, 15);
            label4.TabIndex = 7;
            label4.Text = "Image to decrypt";
            // 
            // pasteBtn
            // 
            pasteBtn.Location = new Point(668, 245);
            pasteBtn.Name = "pasteBtn";
            pasteBtn.Size = new Size(61, 28);
            pasteBtn.TabIndex = 6;
            pasteBtn.Text = "Paste";
            pasteBtn.UseVisualStyleBackColor = true;
            pasteBtn.Click += pasteBtn_Click;
            // 
            // encryptKey
            // 
            encryptKey.Location = new Point(581, 160);
            encryptKey.Name = "encryptKey";
            encryptKey.Size = new Size(148, 79);
            encryptKey.TabIndex = 5;
            encryptKey.Text = "";
            // 
            // copyBtn
            // 
            copyBtn.Location = new Point(1210, 407);
            copyBtn.Name = "copyBtn";
            copyBtn.Size = new Size(75, 28);
            copyBtn.TabIndex = 4;
            copyBtn.Text = "Copy";
            copyBtn.UseVisualStyleBackColor = true;
            copyBtn.Click += copyBtn_Click;
            // 
            // decryptBtn
            // 
            decryptBtn.Location = new Point(581, 429);
            decryptBtn.Name = "decryptBtn";
            decryptBtn.Size = new Size(148, 51);
            decryptBtn.TabIndex = 3;
            decryptBtn.Text = "Decrypt";
            decryptBtn.UseVisualStyleBackColor = true;
            decryptBtn.Click += decryptBtn_Click;
            // 
            // inputBtn
            // 
            inputBtn.Location = new Point(276, 407);
            inputBtn.Name = "inputBtn";
            inputBtn.Size = new Size(93, 28);
            inputBtn.TabIndex = 2;
            inputBtn.Text = "Input image";
            inputBtn.UseVisualStyleBackColor = true;
            inputBtn.Click += inputBtn_Click;
            // 
            // outputText
            // 
            outputText.Location = new Point(940, 56);
            outputText.Name = "outputText";
            outputText.Size = new Size(345, 345);
            outputText.TabIndex = 1;
            outputText.Text = "";
            // 
            // inputImage
            // 
            inputImage.BorderStyle = BorderStyle.FixedSingle;
            inputImage.Location = new Point(24, 56);
            inputImage.Name = "inputImage";
            inputImage.Size = new Size(345, 345);
            inputImage.SizeMode = PictureBoxSizeMode.Zoom;
            inputImage.TabIndex = 0;
            inputImage.TabStop = false;
            // 
            // Form1
            // 
            AutoScaleDimensions = new SizeF(7F, 15F);
            AutoScaleMode = AutoScaleMode.Font;
            ClientSize = new Size(1346, 658);
            Controls.Add(tabControl1);
            Name = "Form1";
            Text = "Form1";
            Load += Form1_Load;
            tabControl1.ResumeLayout(false);
            tabPage1.ResumeLayout(false);
            tabPage1.PerformLayout();
            ((System.ComponentModel.ISupportInitialize)outputImage).EndInit();
            tabPage2.ResumeLayout(false);
            tabPage2.PerformLayout();
            ((System.ComponentModel.ISupportInitialize)inputImage).EndInit();
            ResumeLayout(false);
        }

        private void TabControl1_SelectedIndexChanged(object sender, EventArgs e)
        {
            throw new NotImplementedException();
        }

        #endregion
        private TabControl tabControl1;
        private TabPage tabPage1;
        private TabPage tabPage2;
        private PictureBox outputImage;
        private RichTextBox inputText;
        private Button csBtn;
        private Button encryptBtn;
        private TextBox outputKey;
        private Label label3;
        private Label label1;
        private Button copyBtn;
        private Button decryptBtn;
        private Button inputBtn;
        private RichTextBox outputText;
        private PictureBox inputImage;
        private Button pasteBtn;
        private RichTextBox encryptKey;
        private Label label6;
        private Label label5;
        private Label label4;
        private Label label10;
        private Label label9;
        private Label label8;
        private Label label7;
        private TextBox ImageW;
        private TextBox ImageH;
        private Label label2;
        private ComboBox gBox;
        private RadioButton Circle;
        private RadioButton noCircle;
        private ComboBox fBox;
        private Label label11;
    }
}
