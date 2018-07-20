using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using FileWatcherEx;

namespace DemoApp
{
    public partial class Form1 : Form
    {
        public Form1()
        {
            InitializeComponent();
        }

        FileWatcherEx.FileWatcherEx _fw = new FileWatcherEx.FileWatcherEx();


        private void Form1_Load(object sender, EventArgs e)
        {

        }

        private void btnStart_Click(object sender, EventArgs e)
        {
            this._fw = new FileWatcherEx.FileWatcherEx(txtPath.Text.Trim());
            this._fw.Start();
        }

        private void btnStop_Click(object sender, EventArgs e)
        {
            this._fw.Stop();
        }







       
    }
}
