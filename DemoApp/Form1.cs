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

            this._fw.Start();
        }

        private void _fw_OnChanged(FileChangedEvent e)
        {
            MessageBox.Show(string.Format("[cha] {0} | {1}",
                Enum.GetName(typeof(ChangeType), e.ChangeType),
                e.FullPath));
        }

        private void _fw_OnDeleted(FileChangedEvent e)
        {
            MessageBox.Show(string.Format("[del] {0} | {1}",
                Enum.GetName(typeof(ChangeType), e.ChangeType),
                e.FullPath));
        }

        private void _fw_OnCreated(FileChangedEvent e)
        {
            MessageBox.Show(string.Format("[cre] {0} | {1}",
                Enum.GetName(typeof(ChangeType), e.ChangeType),
                e.FullPath));
        }

        private void _fw_OnRenamed(FileChangedEvent e)
        {
            MessageBox.Show(string.Format("[ren] {0} | {1} ----> {2}",
                Enum.GetName(typeof(ChangeType), e.ChangeType),
                e.OldFullPath,
                e.FullPath));
        }




        private void btnStop_Click(object sender, EventArgs e)
        {
            this._fw.Stop();
        }


        private void btnSelectFolder_Click(object sender, EventArgs e)
        {
            FolderBrowserDialog f = new FolderBrowserDialog();


            if(f.ShowDialog() == DialogResult.OK)
            {
                txtPath.Text = f.SelectedPath;
            }
        }
    }
}
