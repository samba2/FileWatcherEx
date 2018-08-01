using System;
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
            this._fw = new FileWatcherEx.FileWatcherEx(txtPath.Text.Trim())
            {

            };

            this._fw.OnRenamed += _fw_OnRenamed;
            this._fw.OnCreated += _fw_OnCreated;
            this._fw.OnDeleted += _fw_OnDeleted;
            this._fw.OnChanged += _fw_OnChanged;
            this._fw.OnError += _fw_OnError;

            this._fw.Start();
        }

        private void _fw_OnError(object sender, System.IO.ErrorEventArgs e)
        {
            if (txtConsole.InvokeRequired)
            {
                txtConsole.Invoke(new Action<Object, System.IO.ErrorEventArgs>(_fw_OnError), sender, e);
            }
            else
            {
                txtConsole.Text += "[ERROR]: " + e.GetException().Message + "\r\n";
            }
        }

        private void _fw_OnChanged(Object sender, FileChangedEvent e)
        {
            if (txtConsole.InvokeRequired)
            {
                txtConsole.Invoke(new Action<Object, FileChangedEvent>(_fw_OnChanged), sender, e);
            }
            else
            {
                txtConsole.Text += string.Format("[cha] {0} | {1}",
                Enum.GetName(typeof(ChangeType), e.ChangeType),
                e.FullPath) + "\r\n";
            }
        }

        private void _fw_OnDeleted(Object sender, FileChangedEvent e)
        {
            if (txtConsole.InvokeRequired)
            {
                txtConsole.Invoke(new Action<Object, FileChangedEvent>(_fw_OnDeleted), sender, e);
            }
            else
            {
                txtConsole.Text += string.Format("[del] {0} | {1}",
                Enum.GetName(typeof(ChangeType), e.ChangeType),
                e.FullPath) + "\r\n";
            }
        }

        private void _fw_OnCreated(Object sender, FileChangedEvent e)
        {
            if (txtConsole.InvokeRequired)
            {
                txtConsole.Invoke(new Action<Object, FileChangedEvent>(_fw_OnCreated), sender, e);
            }
            else
            {
                txtConsole.Text += string.Format("[cre] {0} | {1}",
                Enum.GetName(typeof(ChangeType), e.ChangeType),
                e.FullPath) + "\r\n";
            }
        }

        private void _fw_OnRenamed(Object sender, FileChangedEvent e)
        {
            if (txtConsole.InvokeRequired)
            {
                txtConsole.Invoke(new Action<Object, FileChangedEvent>(_fw_OnRenamed), sender, e);
            }
            else
            {
                txtConsole.Text += string.Format("[ren] {0} | {1} ----> {2}",
                Enum.GetName(typeof(ChangeType), e.ChangeType),
                e.OldFullPath,
                e.FullPath) + "\r\n";
            }
        }


        




        private void btnStop_Click(object sender, EventArgs e)
        {
            this._fw.Stop();
        }


        private void btnSelectFolder_Click(object sender, EventArgs e)
        {
            FolderBrowserDialog f = new FolderBrowserDialog();


            if (f.ShowDialog() == DialogResult.OK)
            {
                txtPath.Text = f.SelectedPath;

                this._fw.Stop();
                this._fw.Dispose();
            }
        }

        private void Form1_FormClosing(object sender, FormClosingEventArgs e)
        {
            this._fw.Stop();
            this._fw.Dispose();
        }
    }
}
