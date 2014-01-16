using System;
using System.Drawing;
using System.Collections;
using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Windows.Forms;
using DirectShowLib;
using DirectShowLib.BDA;
namespace Turkay_DTV
{
    partial class Form2
    {
        /// <summary>
        /// Required designer variable.
        /// </summary>
        private System.ComponentModel.IContainer components = null;

        /// <summary>
        /// Clean up any resources being used.
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
        /// Required method for Designer support - do not modify
        /// the contents of this method with the code editor.
        /// </summary>
        private void InitializeComponent()
        {
            this.comboSigPol = new System.Windows.Forms.ComboBox();
            this.label7 = new System.Windows.Forms.Label();
            this.label6 = new System.Windows.Forms.Label();
            this.textSymbolRate = new System.Windows.Forms.TextBox();
            this.button1 = new System.Windows.Forms.Button();
            this.label5 = new System.Windows.Forms.Label();
            this.label4 = new System.Windows.Forms.Label();
            this.label3 = new System.Windows.Forms.Label();
            this.label2 = new System.Windows.Forms.Label();
            this.label1 = new System.Windows.Forms.Label();
            this.textSID = new System.Windows.Forms.TextBox();
            this.textTSID = new System.Windows.Forms.TextBox();
            this.textONID = new System.Windows.Forms.TextBox();
            this.textCarrierFreq = new System.Windows.Forms.TextBox();
            this.SuspendLayout();
            // 
            // comboSigPol
            // 
            this.comboSigPol.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            this.comboSigPol.Items.AddRange(new object[] {
            "NotDefined",
            "LinearH",
            "LinearV",
            "CircularL",
            "CircularR",
            "Max",
            "NotSet"});
            this.comboSigPol.Location = new System.Drawing.Point(114, 33);
            this.comboSigPol.Name = "comboSigPol";
            this.comboSigPol.Size = new System.Drawing.Size(96, 21);
            this.comboSigPol.TabIndex = 25;
            // 
            // label7
            // 
            this.label7.Location = new System.Drawing.Point(10, 33);
            this.label7.Name = "label7";
            this.label7.Size = new System.Drawing.Size(104, 23);
            this.label7.TabIndex = 37;
            this.label7.Text = "Signal Polarisation";
            // 
            // label6
            // 
            this.label6.Location = new System.Drawing.Point(10, 57);
            this.label6.Name = "label6";
            this.label6.Size = new System.Drawing.Size(104, 23);
            this.label6.TabIndex = 36;
            this.label6.Text = "Symbol Rate";
            // 
            // textSymbolRate
            // 
            this.textSymbolRate.Location = new System.Drawing.Point(114, 57);
            this.textSymbolRate.Name = "textSymbolRate";
            this.textSymbolRate.Size = new System.Drawing.Size(100, 20);
            this.textSymbolRate.TabIndex = 26;
            // 
            // button1
            // 
            this.button1.FlatStyle = System.Windows.Forms.FlatStyle.System;
            this.button1.Location = new System.Drawing.Point(226, 129);
            this.button1.Name = "button1";
            this.button1.Size = new System.Drawing.Size(48, 23);
            this.button1.TabIndex = 30;
            this.button1.Text = "SET";
            this.button1.Click += new System.EventHandler(this.button1_Click);
            // 
            // label5
            // 
            this.label5.Location = new System.Drawing.Point(218, 9);
            this.label5.Name = "label5";
            this.label5.Size = new System.Drawing.Size(32, 23);
            this.label5.TabIndex = 35;
            this.label5.Text = "Khz";
            // 
            // label4
            // 
            this.label4.Location = new System.Drawing.Point(10, 129);
            this.label4.Name = "label4";
            this.label4.Size = new System.Drawing.Size(104, 23);
            this.label4.TabIndex = 34;
            this.label4.Text = "SID";
            // 
            // label3
            // 
            this.label3.Location = new System.Drawing.Point(10, 105);
            this.label3.Name = "label3";
            this.label3.Size = new System.Drawing.Size(104, 23);
            this.label3.TabIndex = 33;
            this.label3.Text = "TSID";
            // 
            // label2
            // 
            this.label2.Location = new System.Drawing.Point(10, 81);
            this.label2.Name = "label2";
            this.label2.Size = new System.Drawing.Size(104, 23);
            this.label2.TabIndex = 32;
            this.label2.Text = "ONID";
            // 
            // label1
            // 
            this.label1.Location = new System.Drawing.Point(10, 9);
            this.label1.Name = "label1";
            this.label1.Size = new System.Drawing.Size(104, 24);
            this.label1.TabIndex = 31;
            this.label1.Text = "Carrier Frequency";
            // 
            // textSID
            // 
            this.textSID.Location = new System.Drawing.Point(114, 129);
            this.textSID.Name = "textSID";
            this.textSID.Size = new System.Drawing.Size(100, 20);
            this.textSID.TabIndex = 29;
            // 
            // textTSID
            // 
            this.textTSID.Location = new System.Drawing.Point(114, 105);
            this.textTSID.Name = "textTSID";
            this.textTSID.Size = new System.Drawing.Size(100, 20);
            this.textTSID.TabIndex = 28;
            // 
            // textONID
            // 
            this.textONID.Location = new System.Drawing.Point(114, 81);
            this.textONID.Name = "textONID";
            this.textONID.Size = new System.Drawing.Size(100, 20);
            this.textONID.TabIndex = 27;
            // 
            // textCarrierFreq
            // 
            this.textCarrierFreq.Location = new System.Drawing.Point(114, 9);
            this.textCarrierFreq.Name = "textCarrierFreq";
            this.textCarrierFreq.Size = new System.Drawing.Size(100, 20);
            this.textCarrierFreq.TabIndex = 24;
            // 
            // Form2
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(284, 171);
            this.Controls.Add(this.comboSigPol);
            this.Controls.Add(this.label7);
            this.Controls.Add(this.label6);
            this.Controls.Add(this.textSymbolRate);
            this.Controls.Add(this.button1);
            this.Controls.Add(this.label5);
            this.Controls.Add(this.label4);
            this.Controls.Add(this.label3);
            this.Controls.Add(this.label2);
            this.Controls.Add(this.label1);
            this.Controls.Add(this.textSID);
            this.Controls.Add(this.textTSID);
            this.Controls.Add(this.textONID);
            this.Controls.Add(this.textCarrierFreq);
            this.Name = "Form2";
            this.Text = "Form2";
            this.Load += new System.EventHandler(this.Form2_Load);
            this.ResumeLayout(false);
            this.PerformLayout();

        }

        #endregion

        private System.Windows.Forms.ComboBox comboSigPol;
        private System.Windows.Forms.Label label7;
        private System.Windows.Forms.Label label6;
        private System.Windows.Forms.TextBox textSymbolRate;
        private System.Windows.Forms.Button button1;
        private System.Windows.Forms.Label label5;
        private System.Windows.Forms.Label label4;
        private System.Windows.Forms.Label label3;
        private System.Windows.Forms.Label label2;
        private System.Windows.Forms.Label label1;
        private System.Windows.Forms.TextBox textSID;
        private System.Windows.Forms.TextBox textTSID;
        private System.Windows.Forms.TextBox textONID;
        private System.Windows.Forms.TextBox textCarrierFreq;

    }
}