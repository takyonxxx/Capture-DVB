using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Windows.Forms;

namespace Turkay_DTV
{
    public partial class Form2 : Form
    {
        public Form2()
        {
            InitializeComponent();
        }
        public string stextCarrierFreq { get; set; }
        public string scomboSigPol { get; set; }
        public string stextSymbolRate { get; set; }
        public string stextONID { get; set; }
        public string stextTSID { get; set; }
        public string stextSID { get; set; }
        private void button1_Click(object sender, EventArgs e)
        {
            stextCarrierFreq = textCarrierFreq.Text;
            scomboSigPol = comboSigPol.Text;
            stextSymbolRate = textSymbolRate.Text;
            stextONID = textONID.Text;
            stextTSID = textTSID.Text;
            stextSID = textSID.Text;
            this.DialogResult = DialogResult.OK;
            this.Close();
           
        }

        private void Form2_Load(object sender, EventArgs e)
        {
            textCarrierFreq.Text=stextCarrierFreq;
            comboSigPol.SelectedIndex = comboSigPol.FindStringExact(scomboSigPol);
            textSymbolRate.Text=stextSymbolRate;
            textONID.Text = stextONID;
            textTSID.Text= stextTSID;
            textSID.Text = stextSID;

        }
       
    }
}
