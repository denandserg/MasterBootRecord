using Microsoft.Win32.SafeHandles;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace MBR
{
    public partial class Form1 : Form
    {
        const uint GENERIC_READ = 0x80000000;
        const uint FILE_SHARE_READ = 0x1;
        const uint FILE_SHARE_WRITE = 0x2;
        const uint OPEN_EXISTING = 0x3;
        const uint FILE_ATTRIBUTE_NORMAL = 0x80;
        const uint ERROR_INSUFFICIENT_BUFFER = 0x7A;
        const int WM_DEVICECHANGE = 0x219;
        const int DBT_DEVICEARRIVAL = 0x8000;
        const int DBT_DEVICEREMOVECOMPLETE = 0x8004;


        internal static readonly IntPtr INVALID_HANDLE_VALUE = new IntPtr(-1); // WinBase.h

        [DllImport("Kernel32.dll", EntryPoint = "GetLastError", CallingConvention = CallingConvention.Winapi,
            CharSet = CharSet.Unicode)]
        extern static int GetLastError();

        [DllImport("Kernel32.dll")]
        static extern int QueryDosDevice(string lpDeviceName, IntPtr lpTargetPath, int ucchMax);

        [DllImport("Kernel32.dll", CharSet = CharSet.Ansi)]
        extern static IntPtr CreateFile(
            string strPath,
            uint dwDesiredAccess,
            uint dwShareMode,
            IntPtr lpSecurityAttributes,
            uint dwCreationDisposition,
            uint dwFlagsAndAttributes,
            IntPtr hTemplateFile);

        private static string[] QueryDosDevice()
        {
            // Allocate some memory to get a list of all system devices.
            // Start with a small size and dynamically give more space until we have enough room.
            int returnSize = 0;
            int maxSize = 100;
            string allDevices = null;
            IntPtr mem;
            string[] retval = null;

            while (returnSize == 0)
            {
                mem = Marshal.AllocHGlobal(maxSize);
                if (mem != IntPtr.Zero)
                {
                    // mem points to memory that needs freeing
                    try
                    {
                        returnSize = QueryDosDevice(null, mem, maxSize);
                        if (returnSize != 0)
                        {
                            allDevices = Marshal.PtrToStringAnsi(mem, returnSize);
                            retval = allDevices.Split('\0');
                            break; // not really needed, but makes it more clear...
                        }
                        else if (GetLastError() == ERROR_INSUFFICIENT_BUFFER)
                            //maybe better
                            //else if( Marshal.GetLastWin32Error() == ERROR_INSUFFICIENT_BUFFER)
                            //ERROR_INSUFFICIENT_BUFFER = 122;
                        {
                            maxSize *= 10;
                        }
                        else
                        {
                            Marshal.ThrowExceptionForHR(GetLastError());
                        }
                    }
                    finally
                    {
                        Marshal.FreeHGlobal(mem);
                    }
                }
                else
                {
                    throw new OutOfMemoryException();
                }
            }

            return retval;
        }

        public Form1()
        {
            InitializeComponent();
        }

        private void button1_Click(object sender, EventArgs e)
        {
            IntPtr hFile = CreateFile(@"\\.\" + comboBox1.SelectedItem.ToString(),
                GENERIC_READ,
                FILE_SHARE_READ | FILE_SHARE_WRITE,
                IntPtr.Zero,
                OPEN_EXISTING,
                FILE_ATTRIBUTE_NORMAL,
                IntPtr.Zero);
            if (hFile == INVALID_HANDLE_VALUE)
            {
                MessageBox.Show(GetLastError().ToString());
                return;
            }

            using (FileStream driveStream =
                new FileStream(new SafeFileHandle(hFile, true), FileAccess.Read))
            using (FileStream stream =
                new FileStream("mbr.bin", FileMode.OpenOrCreate, FileAccess.Write))
            {
                byte[] buffer = new byte[512];
                int readed = driveStream.Read(buffer, 0, 512);
                if (readed == 512)
                {
                    stream.Write(buffer, 0, readed);
                }
            }

            GetInfoMBR();
        }



        private void Form1_Load(object sender, EventArgs e)
        {
            foreach (var disk in QueryDosDevice())
            {
                if (disk.Length >= 13 && disk.Substring(0, 13).CompareTo("PhysicalDrive") == 0)
                    comboBox1.Items.Add(disk);
            }

            if (comboBox1.Items.Count > 0)
            {
                comboBox1.SelectedIndex = 0;
            }
        }

        private void GetInfoMBR()
        {
            byte[] buffer = new byte[512];
            using (FileStream fileStream = new FileStream("mbr.bin", FileMode.Open))
                fileStream.Read(buffer, 0, 512);
            int num1 = ((IEnumerable<byte>)buffer).Count<byte>() - 2;
            byte[] numArray = buffer;
            int index1 = num1;
            int index2 = index1 + 1;
            bool flag = numArray[index1] == (byte)85 && buffer[index2] == (byte)170;
            byte[] status = new byte[4];
            byte[,] chsAdressFirst = new byte[4, 3];
            byte[,] chsAdressLast = new byte[4, 3];
            byte[,] LBA = new byte[4, 4];
            byte[] type = new byte[4];
            byte[,] numberOfSectors = new byte[4, 4];
            int num2 = 446;
            for (int index3 = 0; index3 < 4; ++index3)
            {
                int index4 = num2 + index3 * 16;
                status[index3] = buffer[index4];
                for (int index5 = 0; index5 < 3; ++index5)
                    chsAdressFirst[index3, index5] = buffer[index4 + index5 + 1];
                type[index3] = buffer[index4 + 4];
                for (int index5 = 0; index5 < 3; ++index5)
                    chsAdressLast[index3, index5] = buffer[index4 + index5 + 5];
                for (int index5 = 0; index5 < 4; ++index5)
                    LBA[index3, index5] = buffer[index4 + index5 + 8];
                for (int index5 = 0; index5 < 4; ++index5)
                    numberOfSectors[index3, index5] = buffer[index4 + index5 + 12];
            }
            this.ShowInfoMBR(new Form1.MBR(flag, status, type, numberOfSectors, chsAdressFirst, chsAdressLast, LBA));
        }

        private void ShowInfoMBR(Form1.MBR mbr)
        {
            string str = "Boot signature is " + (mbr._flag ? "legal" : "illegal");
            this.mbrListView.Items.Clear();
            for (int i = 0; i < 6; ++i)
            {
                ListViewItem listViewItem = new ListViewItem()
                {
                    Text = i == 0 ? str : ""
                };
                for (int y = 0; y < 4; ++y)
                    listViewItem.SubItems.Add(this.GetMBRItem(i, y, mbr));
                this.mbrListView.Items.Add(listViewItem);
            }
        }

        public struct MBR
        {
            public bool _flag;
            public byte[] _status;
            public byte[] _type;
            public byte[,] _chsAdressFirst;
            public byte[,] _chsAdressLast;
            public byte[,] _LBA;
            public byte[,] _numberOfSectors;

            public MBR(bool flag, byte[] status, byte[] type, byte[,] numberOfSectors, byte[,] chsAdressFirst,
                byte[,] chsAdressLast, byte[,] LBA)

            {
                this._flag = flag;
                this._status = status;
                this._type = type;
                this._chsAdressFirst = chsAdressFirst;
                this._chsAdressLast = chsAdressLast;
                this._LBA = LBA;
                this._numberOfSectors = numberOfSectors;
            }

            

        }
        private void SetCombobox()
        {
            int selected = comboBox1.SelectedIndex > -1 ? comboBox1.SelectedIndex : 0;
            comboBox1.Items.Clear();
            foreach (var disk in QueryDosDevice())
            {
                if (disk.Length >= 13 && disk.Substring(0, 13).CompareTo("PhysicalDrive") == 0)
                    comboBox1.Items.Add(disk);
            }

            if (comboBox1.Items.Count > 0)
            {
                int count = comboBox1.Items.Count - 1;
                comboBox1.SelectedIndex = count < selected ? count : selected;
            }

        }

        protected override void WndProc(ref Message m)
        {

            if (m.Msg == WM_DEVICECHANGE && (int)m.WParam == DBT_DEVICEARRIVAL ||
                (int)m.WParam == DBT_DEVICEREMOVECOMPLETE)
            {
                SetCombobox();
            }

            base.WndProc(ref m);
        }

        private void comboBox1_SelectedIndexChanged(object sender, EventArgs e)
        {
            
        }

        private string GetMBRItem(int i, int y, Form1.MBR mbr)
        {
            string str = "";
            switch (i)
            {
                case 0:
                    str = mbr._status[y] != (byte)128 ? (mbr._status[y] != (byte)0 ? "invalid" : "inactive") : "active or bootable";
                    break;
                case 1:
                    str = "CHS Adress First ";
                    for (int index = 0; index < 3; ++index)
                        str += mbr._chsAdressFirst[y, index].ToString();
                    break;
                case 2:
                    str = "Partition type " + (object)mbr._type[y];
                    break;
                case 3:
                    str = "CHS Adress Last ";
                    for (int index = 0; index < 3; ++index)
                        str += mbr._chsAdressLast[y, index].ToString();
                    break;
                case 4:
                    str = "LBA ";
                    for (int index = 0; index < 4; ++index)
                        str += mbr._LBA[y, index].ToString();
                    break;
                case 5:
                    str = "Number of sectors in partition ";
                    for (int index = 0; index < 4; ++index)
                        str += mbr._numberOfSectors[y, index].ToString();
                    break;
            }
            return str;
        }
    }
}
