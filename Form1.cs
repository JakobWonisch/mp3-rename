using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.RegularExpressions;
using System.Windows.Forms;

namespace mp3_rename
{
    public partial class formRenameFiles : Form
    {
        public formRenameFiles()
        {
            InitializeComponent();

            this.Icon = Properties.Resources.shark_ninja_icon;
            this.playing = false;
            CreateMediaPlayer();

            string[] args = Environment.GetCommandLineArgs();
            if (args[args.Length - 1] == "-debug") {
                OpenFiles_Local(); // for local testing
                return;
            }

            OpenFiles();
        }

        private void OpenFiles_Local()
        {
            this.folderPath = Environment.GetFolderPath(Environment.SpecialFolder.MyMusic);
            Directory.SetCurrentDirectory(this.folderPath);
            ReloadFiles();
        }

        private void CreateMediaPlayer()
        {
            string progId = "WMPlayer.OCX.7";
            Type type = Type.GetTypeFromProgID(progId);
            this.player = Activator.CreateInstance(type);
        }

        private string folderPath;
        private string syncFolder;
        private bool playing;
        private dynamic player;

        private void RenameFiles()
        {
            // special case: "25 = 34式太极"
            //
            //  "34太极",
            //  "34式太极",
            //  "25 =34太极",
            //  "25 = 34太极",
            //  "25 = 34式太极",
            //  "25 = 34路太极",
            //  "25 = 34套太极",
            //  "10 - 太极",
            //  "abc",
            //
            string prefix = @"^[-= 0-9a-zA-Z]*";
            Regex reSpecialCase = new Regex(prefix + @"\b([0-9]+[式路套].+)");
            Regex reMain = new Regex(prefix + @"(.+)");

            int n = 0;
            bool changed = false;

            foreach (ListViewItem item in listViewFiles.Items)
            {
                var fileName = item.Text;
                var original = fileName;
                n ++;

                Match m = reSpecialCase.Match(fileName);
                if (m.Success)
                {
                    fileName = m.Groups[1].Value;
                }
                else
                {
                    m = reMain.Match(fileName);
                    if (m.Success)
                    {
                        fileName = m.Groups[1].Value;
                    }
                    else
                    {
                        continue;
                    }
                }

                fileName = n.ToString("D2") + " - " + fileName;

                if (fileName != original)
                {
                    File.Move(original + ".mp3", fileName + ".mp3");
                    changed = true;
                }
            }

            if (!changed)
            {
                return;
            }

            StopPlay();

            if (!RefreshOrdersOnDisk())
            {
                //
                // Refresh one more time, which seems solving majority of problems.
                //
                if (!RefreshOrdersOnDisk())
                {
                    MessageBox.Show("文件没有正确排序", "出错");
                }
            }

            SyncFiles();

            ReloadFiles();
        }

        private bool RefreshOrdersOnDisk()
        {
            //
            // "Many MP3 players that are based on USB flash drives don't allow you to sort the MP3 files
            // in the order you want to listen to them. Instead they play the MP3 files in the order they
            // find them; usually the order you copied them to the flash drive. How do we re-order the files?"
            //
            // https://blogs.msdn.microsoft.com/oldnewthing/20140304-00/?p=1603
            // What order does the DIR command arrange files if no sort order is specified?
            //
            // "But the easy way out is simply to remove all the files from a directory then move file files
            // into the directory in the order you want them enumerated. That way, the first available slot
            // is the one at the end of the directory, so the file entry gets appended."
            //
            string tempDir = GetTempDirectory();
            MoveFiles(".", tempDir);
            MoveFiles(tempDir, ".");
            Directory.Delete(tempDir);

            var files = Directory.GetFiles(this.folderPath, "*.mp3");
            return IsSorted(files);
        }

        /// <summary>
        /// Determines if string array is sorted from A -> Z
        /// </summary>
        public static bool IsSorted(string[] arr)
        {
            for (int i = 1; i < arr.Length; i++)
            {
                if (arr[i - 1].CompareTo(arr[i]) > 0) // If previous is bigger, return false
                {
                    return false;
                }
            }
            return true;
        }

        private string GetTempDirectory()
        {
            var random = new Random();
            while (true)
            {
                string path = "mp3-" + random.Next(9999).ToString("D4") + ".dir";
                if (!Directory.Exists(path))
                {
                    Directory.CreateDirectory(path);
                    return path;
                }
            }
        }

        private void MoveFiles(string sourceDir, string destDir)
        {
            sourceDir = Path.Combine(this.folderPath, sourceDir);
            destDir = Path.Combine(this.folderPath, destDir);

            var files = Directory.GetFiles(sourceDir, "*.mp3");
            Array.Sort(files);

            foreach (var file in files)
            {
                string fileName = Path.GetFileName(file);
                File.Move(file, Path.Combine(destDir, fileName));
            }
        }

        private void SyncFiles()
        {
            string sourceDir = this.folderPath;
            string destDir = this.syncFolder;

            var sourcesFiles = Directory.GetFiles(sourceDir, "*.mp3");
            var destFiles = Directory.GetFiles(destDir, "*.mp3");

            //
            // Remove extra files in destDir
            //
            foreach (var destFile in destFiles)
            {
                string fileName = Path.GetFileName(destFile);
                string sourceFile = Path.Combine(sourceDir, fileName);
                if (!File.Exists(sourceFile))
                {
                    File.Delete(destFile);
                }
            }

            //
            // Copy new or updated files from sourceDir
            //
            foreach (var sourceFile in sourcesFiles)
            {
                string fileName = Path.GetFileName(sourceFile);
                string destFile = Path.Combine(destDir, fileName);
                bool copy = false;

                if (!File.Exists(destFile))
                {
                    copy = true;
                }
                else
                {
                    FileInfo f1 = new FileInfo(sourceFile);
                    FileInfo f2 = new FileInfo(destFile);
                    if (f1.Length != f2.Length)
                    {
                        copy = true;
                    }
                }

                if (copy)
                {
                    File.Copy(sourceFile, destFile);
                }
            }
        }

        private void ReloadFiles()
        {
            listViewFiles.Clear();

            var files = Directory.GetFiles(this.folderPath, "*.mp3");
            Array.Sort(files);
            foreach (var file in files)
            {
                string fileName = Path.GetFileNameWithoutExtension(file);
                listViewFiles.Items.Add(fileName);
            }

            StopPlay();
        }

        [DllImport("kernel32.dll", CharSet = CharSet.Auto)]
        static extern bool GetVolumeInformation(
            string Volume,
            StringBuilder VolumeName,
            uint VolumeNameSize,
            out uint SerialNumber,
            out uint SerialNumberLength,
            out uint flags,
            StringBuilder fs,
            uint fs_size);

        private void OpenFiles()
        {
            //
            // find the first removable drive
            //
            string drive = null;

            DriveInfo[] allDrives = DriveInfo.GetDrives();
            foreach (DriveInfo d in allDrives)
            {
                if (d.IsReady && d.DriveType == DriveType.Removable)
                {
                    drive = d.Name;
                    break;
                }
            }
            if (drive == null)
            {
                return;
            }

            //
            // get the drive's volume name, serial number
            //
            uint serialNum, serialNumLength, flags;
            StringBuilder volumename = new StringBuilder(256);
            StringBuilder fstype = new StringBuilder(256);

            bool ok = GetVolumeInformation(drive, volumename,
                (uint)volumename.Capacity - 1, out serialNum, out serialNumLength,
                out flags, fstype, (uint)fstype.Capacity - 1);
            if (!ok)
            {
                return;
            }

            this.folderPath = drive;

            //
            // sync folder = <my music>\<volume name>\<volume serial number>
            //
            string serialNumHexString = serialNum.ToString("X8");
            string musicFolder = Environment.GetFolderPath(Environment.SpecialFolder.MyMusic);
            this.syncFolder = Path.Combine(musicFolder, volumename.ToString(), serialNumHexString);
            Directory.CreateDirectory(this.syncFolder);
            SyncFiles();

            Directory.SetCurrentDirectory(this.folderPath);

            ReloadFiles();
        }

        private void StartPlay()
        {
            var items = listViewFiles.SelectedItems;
            if (items.Count != 1 || this.playing)
            {
                return;
            }

            this.playing = true;
            buttonPlay.Enabled = false;
            buttonStop.Enabled = true;

            string fileName = items[0].Text;
            string fullName = Path.Combine(this.folderPath, fileName + ".mp3");
            this.player.URL = fullName;
            this.player.controls.play();
        }

        private void StopPlay()
        {
            buttonPlay.Enabled = (listViewFiles.SelectedItems.Count == 1);
            buttonStop.Enabled = false;

            if (!this.playing)
            {
                return;
            }

            this.playing = false;
            this.player.controls.stop();
        }

        private void saveToolStripButton_Click(object sender, EventArgs e)
        {
            RenameFiles();
        }

        private void buttonPlay_Click(object sender, EventArgs e)
        {
            StartPlay();
        }

        private void buttonStop_Click(object sender, EventArgs e)
        {
            StopPlay();
        }

        private void listViewFiles_DoubleClick(object sender, EventArgs e)
        {
            StartPlay();
        }

        private void listViewFiles_ItemSelectionChanged(object sender, ListViewItemSelectionChangedEventArgs e)
        {
            StopPlay();
        }

        private void listViewFiles_AfterLabelEdit(object sender, LabelEditEventArgs e)
        {
            if (e.Label == null || e.Label.Length == 0)
            {
                e.CancelEdit = true;
                return;
            }

            string original = listViewFiles.Items[e.Item].Text;
            string newName = e.Label;
            File.Move(original + ".mp3", newName + ".mp3");
        }
    }
}
