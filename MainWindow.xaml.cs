using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Timers;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using System.Threading;
using System.IO;

namespace NFC_Keyfob_Tester
{
    public partial class MainWindow : Window
    {
        /*
        ================================================================================= 
        Let's make a lot of public variables, half of which we're probably not gonna use.
        Yes, this comment is 100% useful and informative.
        =================================================================================
         */
        public byte[] SendBuff = new byte[263];
        public byte[] SendMessage = new byte[263];
        public byte[] RecvBuff = new byte[263];
        public int retCode, hContext, hCard, Protocol, SendLen, RecvLen, nBytesRet, reqType, Aprotocol, dwProtocol, cbPciLength;
        public StringBuilder sbContent = new StringBuilder();
        public ModWinsCard.SCARD_READERSTATE RdrState;
        public ModWinsCard.SCARD_IO_REQUEST pioSendRequest;
        public int pcchReaders = 0;
        public bool connectionSuccess = false, RdrFound = false, readLock = true, writeLock = true, timerCheck;
        public string error = "", Cmd = "", msg = "", type = "", oldId = "";
        public List<string> Readers = new List<string>();
        public AutoResetEvent ResetApp = new AutoResetEvent(false);
        public System.Timers.Timer rTimer = new System.Timers.Timer();
        public MainWindow()
        {
            InitializeComponent();
            StartApp();
        }
        public void StartApp() // This is the thing that shows everything in the app. It can be called again to "refresh" the app.
        {
            // before anything else context has to be established
            retCode = ModWinsCard.SCardEstablishContext(ModWinsCard.SCARD_SCOPE_USER, 0, 0, ref hContext);
            if (retCode != ModWinsCard.SCARD_S_SUCCESS)
            {
                error = ModWinsCard.GetScardErrMsg(retCode);
                MessageBox.Show(error);
            }
            else
            {
            }

            string ReaderFound = "Reader: found";
            // List PC/SC card readers installed in the system
            retCode = ModWinsCard.SCardListReaders(hContext, null, null, ref pcchReaders);
            if (retCode != ModWinsCard.SCARD_S_SUCCESS)
            {
                RdrFound = false;
                error = ModWinsCard.GetScardErrMsg(retCode);
                ReaderFound = "No readers found. Error: " + error;
            }
            else
            {
                RdrFound = true;
            }
            // Fill reader list
            byte[] readersList = new byte[pcchReaders];

            retCode = ModWinsCard.SCardListReaders(hContext, null, readersList, ref pcchReaders);
            if (retCode != ModWinsCard.SCARD_S_SUCCESS)
            {
                RdrFound = false;
                error = ModWinsCard.GetScardErrMsg(retCode);
                ReaderFound = "No readers found. Error: " + error;
            }
            else
            {
                RdrFound = true;
            }
            this.Dispatcher.Invoke(() => 
            {
                RdrF.Text = "";
                RdrF.Inlines.Add(ReaderFound);
            });
            string rName = "";
            int indx = 0;
            // Turn the reader names into string
            if (RdrFound != false)
            {
                while (readersList[indx] != 0)
                {
                    while (readersList[indx] != 0)
                    {
                        rName += (char)readersList[indx];
                        indx++;
                    }
                    //Add reader name to list
                    Readers.Add(rName);
                    this.Dispatcher.Invoke(() => 
                    { 
                        UsingReader.Text = "";
                        UsingReader.Inlines.Add("Using " + rName); 
                    });
                    rName = "";
                    indx++;
                }
                RdrState = new ModWinsCard.SCARD_READERSTATE();
                RdrState.RdrName = Readers[0];
            }

            // connect to the card
            string Connect = "";
            string Status = "";
            string uMemory = "";
            bool cardC = false;
            if (RdrFound != false)
            {
                retCode = ModWinsCard.SCardConnect(hContext, Readers[0], ModWinsCard.SCARD_SHARE_SHARED,
                                                     ModWinsCard.SCARD_PROTOCOL_T0 | ModWinsCard.SCARD_PROTOCOL_T1, ref hCard, ref Protocol);
                if (retCode != ModWinsCard.SCARD_S_SUCCESS)
                {
                    error = ModWinsCard.GetScardErrMsg(retCode);
                    Connect = "Card not found. Error: " + error + " Does the reader show a green light?";
                }
                else
                {
                    connectionSuccess = true;
                    Connect = "Card found";
                    int pcchReaderLen = 256;
                    int state = 0;
                    byte atr = 0;
                    int atrLen = 255;

                    //get card status
                    retCode = ModWinsCard.SCardStatus(hCard, Readers[0], ref pcchReaderLen, ref state, ref Protocol, ref atr, ref atrLen);

                    if (retCode != ModWinsCard.SCARD_S_SUCCESS)
                    {
                        error = ModWinsCard.GetScardErrMsg(retCode);
                        Status = "Card status: not found. Error: " + error;
                    }
                    else
                    {
                        cardC = true;
                        Status = "Card status: connected";
                    }
                }
            }
            this.Dispatcher.Invoke(() => 
            { 
                ICF.Text = ""; // These set the text for the app. It's important to always delete previous text before adding anything new.
                ICF.Inlines.Add(Connect);
                CardStatus.Text = "";
                CardStatus.Inlines.Add(Status); 
            });

            type = "";
            readLock = true;
            writeLock = true;
            bool memoryEmpty = true;
            if (connectionSuccess != false && cardC != false)
            {
                // This should get the uid of the tag.
                ModWinsCard.SCARD_IO_REQUEST pioSendRequest = new ModWinsCard.SCARD_IO_REQUEST();
                pioSendRequest.dwProtocol = ModWinsCard.SCARD_PROTOCOL_T1;
                pioSendRequest.cbPciLength = System.Runtime.InteropServices.Marshal.SizeOf(typeof(ModWinsCard.SCARD_IO_REQUEST));
                byte[] SendBuff = new byte[] { 0xFF, 0xCA, 0x00, 0x00, 0x00 };
                RecvLen = RecvBuff.Length;
                retCode = ModWinsCard.SCardTransmit(hCard, ref pioSendRequest, ref SendBuff[0], SendBuff.Length, ref pioSendRequest, ref RecvBuff[0], ref RecvLen);

                if (retCode != ModWinsCard.SCARD_S_SUCCESS)
                {
                    Cmd = "Commands unsuccesful. Is the card connected?";
                }
                else
                {
                    Cmd = "ID: " + BitConverter.ToString(RecvBuff.Take(7).ToArray()).Replace("-", ":"); //.Replace is just preference.
                }
                // To find the type of tag that is used, the code below will read the third byte from the fourth block.
                bool TypeFound = false;
                Array.Clear(SendBuff, 0, SendBuff.Length);
                Array.Clear(RecvBuff, 0, RecvBuff.Length);
                pioSendRequest.dwProtocol = ModWinsCard.SCARD_PROTOCOL_T1;
                pioSendRequest.cbPciLength = System.Runtime.InteropServices.Marshal.SizeOf(typeof(ModWinsCard.SCARD_IO_REQUEST));
                SendBuff = new byte[] { 0xFF, 0xB0, 0x00, 0x03, 0x04 };
                RecvLen = RecvBuff.Length;
                retCode = ModWinsCard.SCardTransmit(hCard, ref pioSendRequest, ref SendBuff[0], SendBuff.Length, ref pioSendRequest, ref RecvBuff[0], ref RecvLen);

                if (retCode != ModWinsCard.SCARD_S_SUCCESS)
                {
                }
                else
                {
                    // The byte can tell what the tag type is most of the time, but it can't distinguish between NTAG213 and NTAG203.
                    if ((int)RecvBuff[0] != 99)
                    {
                        if ((int)RecvBuff[2] != 18) { }
                        else { type = Generation(); TypeFound = true; }
                        if ((int)RecvBuff[2] != 62) { }
                        else { type = "NTAG 215"; TypeFound = true; }
                        if ((int)RecvBuff[2] != 109) { }
                        else { type = "NTAG 216"; TypeFound = true; }
                    }
                    else type = "NTAG unclear";
                }
                if (TypeFound == false && RecvBuff[0] != 99)
                {
                    Array.Clear(SendBuff, 0, SendBuff.Length);
                    Array.Clear(RecvBuff, 0, RecvBuff.Length);
                    SendBuff = new byte[] { 0xFF, 0x00, 0x00, 0x00, 0x04, 0xD4, 0x4A, 0x01, 0x00 };
                    RecvLen = RecvBuff.Length;
                    retCode = ModWinsCard.SCardTransmit(hCard, ref pioSendRequest, ref SendBuff[0], SendBuff.Length, ref pioSendRequest, ref RecvBuff[0], ref RecvLen);
                    if (retCode != ModWinsCard.SCARD_S_SUCCESS)
                    {
                    }
                    else
                    {
                        if ((int)RecvBuff[6] != 0) { }
                        else { type = "Mifare Ultralight"; TypeFound = true; }
                        if ((int)RecvBuff[6] != 8) { }
                        else { type = "Mifare 1K"; TypeFound = true; }
                        if ((int)RecvBuff[6] != 9) { }
                        else { type = "Mifare MINI"; TypeFound = true; }
                        if ((int)RecvBuff[6] != 18) { }
                        else { type = "Mifare 4K"; TypeFound = true; }
                        if ((int)RecvBuff[6] != 20) { }
                        else { type = "MifareDESFire"; TypeFound = true; }
                        if ((int)RecvBuff[6] != 28) { }
                        else { type = "JCOP30"; TypeFound = true; }
                        if ((int)RecvBuff[6] != 98) { }
                        else { type = "Gemplus MPCOS"; TypeFound = true; }
                    }
                }
                string tmpStr = "";
                int iBlock = 0;
                for (int i = 0; i < 40; i++)
                {
                    int tmpInt = 0;
                    // Reads and displays the first 40 blocks of the card.
                    Array.Clear(SendBuff, 0, SendBuff.Length);
                    Array.Clear(RecvBuff, 0, RecvBuff.Length);
                    SendBuff = new byte[] { 0xFF, 0xB0, 0x00, (byte)iBlock, 0x04 };
                    RecvLen = RecvBuff.Length;
                    pioSendRequest.dwProtocol = ModWinsCard.SCARD_PROTOCOL_T1;
                    pioSendRequest.cbPciLength = System.Runtime.InteropServices.Marshal.SizeOf(typeof(ModWinsCard.SCARD_IO_REQUEST));
                    retCode = ModWinsCard.SCardTransmit(hCard, ref pioSendRequest, ref SendBuff[0], SendBuff.Length, ref pioSendRequest, ref RecvBuff[0], ref RecvLen);
                    if (retCode != ModWinsCard.SCARD_S_SUCCESS)
                    {
                        error = ModWinsCard.GetScardErrMsg(retCode);
                        uMemory = error;
                        i = int.MaxValue;
                    }
                    else
                    {
                        tmpStr = "";
                        for (indx = 0; indx <= RecvLen - 1; indx++)
                        {
                            tmpStr += " " + string.Format("{0:X2}", RecvBuff[indx]);
                        }
                    }
                    if (tmpStr.Contains("90 00"))
                    {
                        string ascii = "";
                        string n = "[" + string.Format("{0:00}", iBlock) + "] - "; // this is just formatting to make the blocks easy to read
                        tmpStr = tmpStr.Replace("90 00", "");
                        if (iBlock >= 4)
                        {
                            for (int index = 0; index <= 6; index += 2)
                            {
                                string tmp = tmpStr.Replace(" ", "");
                                string hs = tmp.Substring(index, 2);
                                uint dec = System.Convert.ToUInt32(hs.Trim(), 16);
                                char character = System.Convert.ToChar(dec);
                                ascii += character;
                            }
                        }
                        uMemory += n + tmpStr + " - " + ascii + "\n"; // adds them all together for a nice list

                        if (readLock != false) { readLock = false; }
                        if (iBlock != 6) { }
                        else
                        {
                            writeLock = true;
                            byte[] testBuff = new byte[] { RecvBuff[0], RecvBuff[1], RecvBuff[2], RecvBuff[3] };
                            LockTest(testBuff);
                        } // saves the first byte from block 06 so we can use it to test writing later.
                        if (memoryEmpty == false || iBlock < 4 || iBlock > 16)
                        {
                        }
                        else
                        {
                            tmpInt += RecvBuff[0];
                            tmpInt += RecvBuff[1];
                            tmpInt += RecvBuff[2];
                            tmpInt += RecvBuff[3];
                            if (tmpInt > 0 && tmpInt != 144) { memoryEmpty = false; }
                        }

                    }
                    else
                    {
                        uMemory += ("\n");
                    }
                    iBlock++;
                }
            }
            this.Dispatcher.Invoke(() =>
            {
                userMemory.Text = "";
                userMemory.Inlines.Add(uMemory);
                cmdTB.Text = "";
                cmdTB.Inlines.Add(Cmd);
                Change.Text = "";
                if (Cmd != oldId) // if the id has changed or not 
                {
                    Change.Inlines.Add("Tag id changed!");
                    SolidColorBrush brush = new SolidColorBrush();
                    brush.Color = Color.FromArgb(255, 0, 255, 0);
                    Change.Background = brush;
                }
                else { Change.Background = null; }
                oldId = Cmd;
                NTAG.Text = "";
                NTAG.Inlines.Add(type);
            });


            this.Dispatcher.Invoke(() =>
            {
                Lock.Text = "";
                if (readLock == false)
                {
                    Lock.Inlines.Add("Read not locked. ");
                }
                else { Lock.Inlines.Add("Reading was unsuccesful. "); }
                ReadWrite.Fill = null;
                if (writeLock == false)
                {
                    Lock.Inlines.Add("Writing not locked. ");
                    SolidColorBrush brush = new SolidColorBrush();
                    brush.Color = Color.FromArgb(255, 0, 255, 0);
                    ReadWrite.Fill = brush;
                }
                else
                {
                    Lock.Inlines.Add("Writing was unsuccesful. ");
                    SolidColorBrush brush = new SolidColorBrush();
                    brush.Color = Color.FromArgb(255, 255, 0, 0);
                    ReadWrite.Fill = brush;
                }

                eMemory.Fill = null;
                if (memoryEmpty == true)
                {
                    SolidColorBrush brush = new SolidColorBrush();
                    brush.Color = Color.FromArgb(255, 0, 255, 0);
                    eMemory.Fill = brush;
                }
                else
                {
                    SolidColorBrush brush = new SolidColorBrush();
                    brush.Color = Color.FromArgb(255, 255, 0, 0);
                    eMemory.Fill = brush;
                }

                TagT.Fill = null;
                if (type != "NTAG unclear" && !string.IsNullOrEmpty(type))
                {
                    SolidColorBrush brush = new SolidColorBrush();
                    brush.Color = Color.FromArgb(255, 0, 255, 0);
                    TagT.Fill = brush;
                }
                else
                {
                    SolidColorBrush brush = new SolidColorBrush();
                    brush.Color = Color.FromArgb(255, 255, 0, 0);
                    TagT.Fill = brush;
                }
            });
        }

        private void LockTest(byte[] tstByte)
        {
            byte[] rstrByte = new byte[] { tstByte[0], tstByte[1], tstByte[2], tstByte[3] };
            // This will write over a block and checks if it changes. After that it returns it to its original value.
            for (int i = 0; i < tstByte.Length; i++)
            {
                if (tstByte[i] >= 255) { tstByte[i] = 0; } // this adds +1 to tstByte unless its at byte max value where it makes it a 0  
                else { tstByte[i]++; }
            }
            int iBlock = 6;
            string sBlock = iBlock.ToString();
            Array.Clear(SendBuff, 0, SendBuff.Length);
            Array.Clear(RecvBuff, 0, RecvBuff.Length);
            SendBuff = new byte[] { 0xff, 0xD6, 0x00, (byte)int.Parse(sBlock), 0x04, tstByte[0], tstByte[1], tstByte[2], tstByte[3] };
            pioSendRequest.dwProtocol = ModWinsCard.SCARD_PROTOCOL_T1;
            pioSendRequest.cbPciLength = System.Runtime.InteropServices.Marshal.SizeOf(typeof(ModWinsCard.SCARD_IO_REQUEST));
            SendLen = SendBuff[4] + 5;
            RecvLen = 0x02;
            retCode = ModWinsCard.SCardTransmit(hCard, ref pioSendRequest, ref SendBuff[0], SendLen, ref pioSendRequest, ref RecvBuff[0], ref RecvLen);
            if (retCode != ModWinsCard.SCARD_S_SUCCESS)
            {
                error = ModWinsCard.GetScardErrMsg(retCode);
                MessageBox.Show($"Error {error}");
            }
            else
            {
                Array.Clear(SendBuff, 0, SendBuff.Length);
                Array.Clear(RecvBuff, 0, RecvBuff.Length);
                SendBuff = new byte[] { 0xFF, 0xB0, 0x00, 0x05, 0x04 };
                RecvLen = RecvBuff.Length;
                pioSendRequest.dwProtocol = ModWinsCard.SCARD_PROTOCOL_T1;
                pioSendRequest.cbPciLength = System.Runtime.InteropServices.Marshal.SizeOf(typeof(ModWinsCard.SCARD_IO_REQUEST));
                retCode = ModWinsCard.SCardTransmit(hCard, ref pioSendRequest, ref SendBuff[0], SendBuff.Length, ref pioSendRequest, ref RecvBuff[0], ref RecvLen);
                if (retCode != ModWinsCard.SCARD_S_SUCCESS)
                {
                    error = ModWinsCard.GetScardErrMsg(retCode);
                    MessageBox.Show($"Error {error}");
                }
                else
                {
                    if (RecvBuff[0] != tstByte[0] && RecvBuff[0] != 99) // if writing was succesful
                    {
                        writeLock = false;
                    }
                    iBlock = 6;
                    sBlock = iBlock.ToString();
                    Array.Clear(SendBuff, 0, SendBuff.Length);
                    Array.Clear(RecvBuff, 0, RecvBuff.Length);
                    SendBuff = new byte[] { 0xff, 0xD6, 0x00, (byte)int.Parse(sBlock), 0x04, rstrByte[0], rstrByte[1], rstrByte[2], rstrByte[3] };
                    pioSendRequest.dwProtocol = ModWinsCard.SCARD_PROTOCOL_T1;
                    pioSendRequest.cbPciLength = System.Runtime.InteropServices.Marshal.SizeOf(typeof(ModWinsCard.SCARD_IO_REQUEST));
                    SendLen = SendBuff[4] + 5;
                    RecvLen = 0x02;
                    retCode = ModWinsCard.SCardTransmit(hCard, ref pioSendRequest, ref SendBuff[0], SendLen, ref pioSendRequest, ref RecvBuff[0], ref RecvLen); // returns the block to its original value.
                    if (retCode != ModWinsCard.SCARD_S_SUCCESS)
                    {
                        error = ModWinsCard.GetScardErrMsg(retCode);
                        MessageBox.Show($"Error {error}");

                    }
                }
            }
        }
        private void Delete_Click(object sender, RoutedEventArgs e)
        {
            if (connectionSuccess != false)
            {
                int iBlock = 4;
                int iLength = 222;
                // this is the amount of writable blocks in NTAG 216. It was chosen as default because it doesn't take too long to process, but still clears the memory (most of the time) if the type is unclear.
                // there is a risk that this could lock the card, but if you try to delete memory from an unknown card that's kind of on you.
                if (type != "NTAG 213") { } // I do it this way because != is faster than ==
                else { iLength = 36; }
                if (type != "NTAG 203") { }
                else { iLength = 36; }
                if (type != "Mifare Ultralight") { }
                else { iLength = 12; }
                if (type != "NTAG 215") { }
                else { iLength = 126; }
                if (type != "NTAG 216") { }
                else { iLength = 222; }
                if (type != "Mifare 1K") { }
                else { iLength = 250; }
                if (type != "Mifare 4K") { }
                else { iLength = 1012; }
                if (type != "Mifare MINI") { }
                else { iLength = 80; }
                if (type != "Mifare 4K") { }
                else { iLength = 1012; }
                for (int i = 0; i < iLength; i++)
                {
                    string sBlock = iBlock.ToString();
                    Array.Clear(SendBuff, 0, SendBuff.Length);
                    Array.Clear(RecvBuff, 0, RecvBuff.Length);
                    SendBuff = new byte[] { 0xff, 0xD6, 0x00, (byte)int.Parse(sBlock), 0x04, 0x00, 0x00, 0x00, 0x00 };
                    pioSendRequest.dwProtocol = ModWinsCard.SCARD_PROTOCOL_T1;
                    pioSendRequest.cbPciLength = System.Runtime.InteropServices.Marshal.SizeOf(typeof(ModWinsCard.SCARD_IO_REQUEST));
                    SendLen = SendBuff[4] + 5;
                    RecvLen = 0x02;
                    retCode = ModWinsCard.SCardTransmit(hCard, ref pioSendRequest, ref SendBuff[0], SendLen, ref pioSendRequest, ref RecvBuff[0], ref RecvLen);
                    if (retCode != ModWinsCard.SCARD_S_SUCCESS)
                    {
                        error = ModWinsCard.GetScardErrMsg(retCode);
                        MessageBox.Show($"Error {error}");
                        break;
                    }
                    iBlock++;
                }
            }
            Thread.Sleep(250);
            StartApp(); // refreshes the information after deletion
        }
        private string Generation()
        {
            // tries to read from block 44, if its not found the tag is labeled as 203
            // this function should only be called if its established that the tag is 213 or 203
            string tmpStr = "";
            int i = 44;
            Array.Clear(SendBuff, 0, SendBuff.Length);
            Array.Clear(RecvBuff, 0, RecvBuff.Length);
            SendBuff = new byte[] { 0xFF, 0xB0, 0x00, (byte)i, 0x04 };
            RecvLen = RecvBuff.Length;
            pioSendRequest.dwProtocol = ModWinsCard.SCARD_PROTOCOL_T1;
            pioSendRequest.cbPciLength = System.Runtime.InteropServices.Marshal.SizeOf(typeof(ModWinsCard.SCARD_IO_REQUEST));
            retCode = ModWinsCard.SCardTransmit(hCard, ref pioSendRequest, ref SendBuff[0], SendBuff.Length, ref pioSendRequest, ref RecvBuff[0], ref RecvLen);
            if (retCode != ModWinsCard.SCARD_S_SUCCESS)
            {
                error = ModWinsCard.GetScardErrMsg(retCode);
            }
            else
            {
                tmpStr = "";
                for (int indx = 0; indx <= RecvLen - 1; indx++)
                {
                    tmpStr += " " + string.Format("{0:X2}", RecvBuff[indx]);
                }
            }
            if (tmpStr.Contains("90 00"))
            {
                return "NTAG 213";
            }
            else
            {
                return "NTAG 203";
            }
        }
        private void Write_Click(object sender, RoutedEventArgs e) // TODO: make this work
        {
            if (connectionSuccess != false)
            {
                string InputText = "";
                this.Dispatcher.Invoke(() => { InputText = Input.Text; });
                byte[] Cnvrtd = Encoding.Default.GetBytes(InputText);
                int length = Cnvrtd.Length % 4;
                if (length != 0)
                {
                    byte[] tmp = new byte[Cnvrtd.Length + 4 - length];
                    Cnvrtd.CopyTo(tmp, 0);
                    for (int i = 0; i < 4 - length; i++)
                    {
                        tmp[Cnvrtd.Length + i] = 00;
                    }
                    Cnvrtd = tmp;
                    // in short this loop just adds zeros to the end of the byte[] if its not divisible by 4 (the amount of writable bytes per block in most cards)
                }
                int iBlock = 4;
                int iLength = 222;
                // this is the amount of writable blocks in NTAG 216. It was chosen as default because it doesn't take too long to process, but still clears the memory (most of the time) if the type is unclear.
                // there is a risk that this could lock the card, but if you try to delete memory from an unknown card that's kind of on you.
                if (type != "NTAG 213") { }
                else { iLength = 36; }
                if (type != "NTAG 203") { }
                else { iLength = 36; }
                if (type == "Mifare Ultralight") { iLength = 12; }
                else { }
                if (type != "NTAG 215") { }
                else { iLength = 126; }
                if (type != "NTAG 216") { }
                else { iLength = 222; }
                if (type != "Mifare 1K") { }
                else { iLength = 250; }
                if (type != "Mifare 4K") { }
                else { iLength = 1012; }
                if (type != "Mifare MINI") { }
                else { iLength = 80; }
                if (type != "Mifare 4K") { }
                else { iLength = 1012; }
                if (iLength < Cnvrtd.Length / 4)
                {
                    MessageBox.Show("Card size is too small to write that input");
                }
                else
                {
                    for (int i = 0; i < Cnvrtd.Length;)
                    {
                        string sBlock = iBlock.ToString();
                        Array.Clear(SendBuff, 0, SendBuff.Length);
                        Array.Clear(RecvBuff, 0, RecvBuff.Length);
                        SendBuff = new byte[] { 0xff, 0xD6, 0x00, (byte)int.Parse(sBlock), 0x04, Cnvrtd[i++], Cnvrtd[i++], Cnvrtd[i++], Cnvrtd[i++] };
                        pioSendRequest.dwProtocol = ModWinsCard.SCARD_PROTOCOL_T1;
                        pioSendRequest.cbPciLength = System.Runtime.InteropServices.Marshal.SizeOf(typeof(ModWinsCard.SCARD_IO_REQUEST));
                        SendLen = SendBuff[4] + 5;
                        RecvLen = 0x02;
                        retCode = ModWinsCard.SCardTransmit(hCard, ref pioSendRequest, ref SendBuff[0], SendLen, ref pioSendRequest, ref RecvBuff[0], ref RecvLen);
                        if (retCode != ModWinsCard.SCARD_S_SUCCESS)
                        {
                            error = ModWinsCard.GetScardErrMsg(retCode);
                            MessageBox.Show($"Error {error}");
                            break;
                        }
                        iBlock++;
                    }
                }
            }
            StartApp(); // refreshes the information after
        }

        private void Timer_Checked(object sender, RoutedEventArgs e)
        {
            // Creates and runs a timer to refresh the app.
            rTimer = new System.Timers.Timer(2500);
            rTimer.Elapsed += OnTimedEvent;
            rTimer.AutoReset = true;
            rTimer.Enabled = true;

        }
        private void Timer_Unchecked(object sender, RoutedEventArgs e)
        {
            // Disables timer
            rTimer.Enabled = false;
        }
        private void OnTimedEvent(Object source, ElapsedEventArgs e)
        {
            StartApp();
        }

        private void Refresh_Click(object sender, RoutedEventArgs e)
        {
            StartApp();
        }
    }
}
