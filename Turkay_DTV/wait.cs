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
    public partial class wait : Form
    {
        public wait()
        {
            InitializeComponent();
        }
       
        public int ProgressValue
        {
            get { return progressBar1.Value; }
            set { progressBar1.Value = value;}
        }       
        
        
    }
}
