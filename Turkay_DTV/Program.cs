using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows.Forms;

namespace Turkay_DTV
{
    static class Program
    {
        /// <summary>
        /// The main entry point for the application.
        /// </summary>
        [STAThread]
        static void Main()
        {
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
           // Class1.Run(new string[]{});
            Application.Run(new Form1());
        }
    }
}
