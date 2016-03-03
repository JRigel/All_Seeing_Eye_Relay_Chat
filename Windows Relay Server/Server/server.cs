using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Linq;
using System.Text;
using System.Diagnostics;
using System.Threading;
using System.IO;
using System.IO.Compression;
using System.Management;
using System.Net;
using System.Net.Sockets;
using System.Runtime.Serialization.Formatters.Binary;
using System.Collections;
using System.Timers;
using System.Text.RegularExpressions;
using System.Runtime.InteropServices;
using System.Web;
using NDde.Server;
using Newtonsoft.Json;

namespace DDE_Server
{

    public class who
    {
        public string address { get; set; }
        public string name { get; set; }
        public string chanlist { get; set; }
        public string server { get; set; }
        public string state { get; set; }
        public string idle { get; set; }
        public string time { get; set; }
        public string auth { get; set; }
    }

    public class send_info
    {
        public string type { get; set; }
        public string timestamp { get; set; }
        public string chan { get; set; }
        public string nick { get; set; }
        public string msg { get; set; }
    }

    public class jData
    {
        public jData() { }
        public string message { get; set; }
    }

    public partial class GCM
    {
        public static void request(string type, string ts, string chan, string nick, string msg, string regid)
        {
            int euckrCodepage = 51949;
            System.Text.Encoding euckr = System.Text.Encoding.GetEncoding(euckrCodepage);
            WebRequest tRequest;
            tRequest = WebRequest.Create("https://android.googleapis.com/gcm/send");
            tRequest.Method = "post";
            tRequest.ContentType = "application/x-www-form-urlencoded";
            tRequest.Headers.Add(string.Format("Authorization: key={0}", "AIzaSyALoT47uhqIB40v4Lj-Z1xErTxZbIYZ138"));
            String collaspeKey = Guid.NewGuid().ToString("n");
            if (regid == null) return;
            String postData = string.Format("registration_id={0}&data.type={1}&data.timestamp={2}&data.chan={3}&data.nick={4}&data.msg={5}&collapse_key={6}", // �޼��� ����
                regid, System.Web.HttpUtility.UrlEncode(type), System.Web.HttpUtility.UrlEncode(ts), System.Web.HttpUtility.UrlEncode(chan),
                System.Web.HttpUtility.UrlEncode(nick), System.Web.HttpUtility.UrlEncode(msg), collaspeKey); // �޼��� ����
            Byte[] byteArray = Encoding.UTF8.GetBytes(postData);
            //Byte[] byteArray = euckr.GetBytes(postData);
            tRequest.ContentLength = byteArray.Length;
            Stream dataStream = tRequest.GetRequestStream();
            dataStream.Write(byteArray, 0, byteArray.Length);
            dataStream.Close();
            WebResponse tResponse = tRequest.GetResponse();
            dataStream = tResponse.GetResponseStream();
            StreamReader tReader = new StreamReader(dataStream);
            String sResponseFromServer = tReader.ReadToEnd();
            Console.WriteLine("GCM PUSH : {0} {1} {2} {3} {4}", type, ts, chan, nick, msg);
            Console.WriteLine("GCM REG ID : " + regid);
            tReader.Close();
            dataStream.Close();
            tResponse.Close();
        }
    }

    public class ClientHandler
    {
        public static Socket clientSocket;
        public static NetworkStream stream;
        public static StreamWriter writer;
        public static send_info i_list;
        public static who whois;
        public static byte[] receiveData;
        public static jData info;
        public static string str;
        public static string send_data;
        public static byte[] Data;
        public static string regid;
        
        public ClientHandler(Socket cs)
        {
            clientSocket = cs;
            DDE_Client d_client = new DDE_Client();
            Thread dc = new Thread(d_client.Run);
            dc.Start(); // DDE Ŭ���̾�Ʈ ����
        }

        public static int byteArrayDefrag(byte[] sData)
        {
            int endLength = 0;
            for (int i = 0; i < sData.Length; i++)
                if ((byte)sData[i] != (byte)0) endLength = i;
            return endLength;
        }

        [DllImport("kernel32.dll")]
        internal static extern Boolean AllocConsole();
        [DllImport("kernel32.dll")]
        internal static extern Boolean FreeConsole();
        [DllImport("kernel32.dll")]
        internal static extern IntPtr GetConsoleWindow();
        [DllImport("user32.dll")]
        internal static extern IntPtr SetParent(IntPtr hWndChild, IntPtr hWndNewParent);     
        [STAThread]
        public static void Main(string[] args)
        {
            if (args.Contains("debug"))
            {
                //������ ����� ���
                AllocConsole();
                IntPtr consoleWindow = GetConsoleWindow();
                Console.Title = "[DEBUG] DDE Relay Server";
            }

            // �ߺ� ���� ����
            bool isNew;
            Mutex mutex = new Mutex(true, "DDE_Server", out isNew);

            if (isNew) mutex.ReleaseMutex();
            else Environment.Exit(0);

            try
            {
                using (DdeServer server = new MyServer("asirc_server")) // DDE ���� ����
                {
                    DDE_Client d_client = new DDE_Client();
                    Thread dc2 = new Thread(d_client.Run2);
                    dc2.Start(); // DDE Ŭ���̾�Ʈ ����

                    IRC_Socket irc_socket = new IRC_Socket();
                    Thread isocket = new Thread(irc_socket.Run);
                    isocket.Start(); // ���� ����

                    server.Register();
                    while (true) { }
                }
            }
            catch (Exception e)
            {
                Console.WriteLine(e.ToString());
                Environment.Exit(0);
            }
        }

        private sealed class MyServer : DdeServer
        {
            Socket client;
            private System.Timers.Timer _Timer = new System.Timers.Timer();
            public MyServer(string service)
                : base(service)
            {
                whois = new who();
                i_list = new send_info();
                _Timer.Elapsed += this.OnTimerElapsed;
                _Timer.Interval = 1000;
                _Timer.SynchronizingObject = this.Context;
            }

            private void OnTimerElapsed(object sender, ElapsedEventArgs args)
            {
                // Advise all topic name and item name pairs.
                Advise("*", "*");
            }

            public override void Register()
            {
                base.Register();
                _Timer.Start();
            }

            public override void Unregister()
            {
                _Timer.Stop();
                base.Unregister();
            }

            protected override PokeResult OnPoke(DdeConversation conversation, string item, byte[] data, int format)
            { //IRC���� ������
                int getValueLength = byteArrayDefrag(data);
                //string AndroData = item + " " + Encoding.UTF8.GetString(data, 0, getValueLength + 1); // ����
                string AndroData = item + " " + Encoding.GetEncoding("utf-8").GetString(data, 0, getValueLength + 1); // ����

                string[] tmp = AndroData.Split(' ', ''); // DDE�� ���� ���� �����͸� ����
                string[] tmp2 = new string[1024];

                if (item == "[����]")
                {
                    client = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
                    client.Connect("210.118.69.193", 5001); //���� ���� �ּ�
                    string authkey = tmp[1] + "+" + Dns.GetHostAddresses(Dns.GetHostName())[3] + "\r\n";
                    byte[] key = Encoding.Default.GetBytes(authkey);
                    client.Send(key);
                    Console.WriteLine("���� �õ� ��");
                    byte[] resp = new byte[1024];
                    try { client.Receive(resp); }
                    catch { }
                    string auth_check = Encoding.UTF8.GetString(resp).Replace("\0", "").Replace("\r", "\r\n");
                    if (auth_check.Contains("success"))
                    {
                        string[] reg = auth_check.Split('&');
                        Properties.Settings.Default.gcm_id  = reg[1];
                        Properties.Settings.Default.gcm_msg = "auth_success";
                        Console.WriteLine("���� �Ϸ� : " + reg[1]);
                    }
                    else if (auth_check.Contains("fail")) Properties.Settings.Default.gcm_msg = "auth_fail";
                    Properties.Settings.Default.Save();
                    client.Shutdown(SocketShutdown.Both);
                    client.Close();
                }

                else if (item == "[��������]")
                {
                    //client = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
                    //client.Connect("210.118.69.193", 5001); //���� ���� �ּ�
                    //string authkey = Dns.GetHostAddresses(Dns.GetHostName())[3] + "\r\n";
                    //byte[] key = Encoding.Default.GetBytes(authkey);
                    //client.Send(key);
                    //Console.WriteLine("�������� �õ� ��");
                    //byte[] resp = new byte[10];
                    //try { client.Receive(resp); }
                    //catch { }
                    //string auth_check = Encoding.UTF8.GetString(resp);
                    //if (auth_check.Contains("deleted"))
                    //{
                        GCM.request(item, null, null, null, null, Properties.Settings.Default.gcm_id);
                        Properties.Settings.Default.gcm_msg = "auth_cancel";
                        Properties.Settings.Default.gcm_id = null;
                        Console.WriteLine("�������� �Ϸ�");
                    //}
                    //else if (auth_check.Contains("fail")) Properties.Settings.Default.gcm_msg = "auth_fail";
                    //Properties.Settings.Default.Save();
                    //client.Shutdown(SocketShutdown.Both);
                    //client.Close();
                }

                else if (item == "[����������]")
                {
                    Properties.Settings.Default.gcm_msg = "auth_cancel";
                    Properties.Settings.Default.gcm_id = null;
                    Console.WriteLine("�������� �Ϸ�");
                }

                else if (item == "[����]")
                {
                    string[] Adata = AndroData.Split('��'); // DDE�� ���� ���� �����͸� ����
                    int cnt = Adata.Count();

                    //�ּ�(����)
                    string[] address = Adata[0].Split(':');
                    whois.address = address[1];

                    //����ڸ�(����)
                    string[] name = Adata[1].Split(':');
                    whois.name = name[1];

                    //ä��
                    string[] chan = Adata[2].Split(':');
                    if (chan[0].Contains("ä��")) whois.chanlist = chan[1];
                    else whois.chanlist = "����";

                    //����
                    string[] server = Adata[3].Split(':');
                    if (server[0].Contains("����")) whois.server = server[1];

                    // ����
                    string[] state = Adata[4].Split(':');
                    if (state[0].Contains("����")) whois.state = state[1];
                    else whois.state = "����";

                    //���޽ð�
                    string[] idle = Adata[5].Split(':');
                    if (idle[0].Contains("���޽ð�")) whois.idle = idle[1];

                    //�����Ͻ�
                    string[] time = Adata[6].Split(':');
                    if (time[0].Contains("�����Ͻ�")) whois.time = time[1];

                    //����
                    string[] auth = Adata[7].Split(':');
                    if (auth[0].Contains("����")) whois.auth = auth[1];
                    else whois.auth = "����";
                }

                else if (tmp[0] == "[ä�θ��]" || tmp[0] == "[����]" || tmp[0] == "[����]" || tmp[0] == "[��ȭ]"
                    || tmp[0] == "[ȣ��]" || tmp[0] == "[��]" || tmp[0] == "[���]" || tmp[0] == "[���̽�]"
                    || tmp[0] == "[���̽�]" || tmp[0] == "[���Ⱥ���]" || tmp[0] == "[ű]" || tmp[0] == "[��]"
                    || tmp[0] == "[���]" || tmp[0] == "[����]")
                {
                    Array.Copy(tmp, tmp2, tmp.Length); // tmp�� tmp2�� ����
                    Array.Clear(tmp2, 0, 4); // tmp2���� �޼����� �����ϰ� ����
                    string msg = string.Join(" ", tmp2, 4, tmp.Length - 4); // ���ڿ� �迭�� ���ڿ��� ����       
                    i_list.type = tmp[0];
                    i_list.timestamp = tmp[1];
                    i_list.chan = tmp[2];
                    i_list.nick = tmp[3];
                    i_list.msg = msg;

                    if (tmp[0] == "[ȣ��]" || tmp[0] == "[����]") // ��ȭ�� �߰��ؾ���
                    {
                        GCM.request(i_list.type, i_list.timestamp, i_list.chan, i_list.nick, i_list.msg, Properties.Settings.Default.gcm_id);
                    }
                }

                else if (tmp[0] == "[����ȭ]")
                {
                    Array.Copy(tmp, tmp2, tmp.Length); // tmp�� tmp2�� ����
                    Array.Clear(tmp2, 0, 3); // tmp2���� �޼����� �����ϰ� ����
                    string msg = string.Join(" ", tmp2, 3, tmp.Length - 3); // ���ڿ� �迭�� ���ڿ��� ����
                    i_list.type = tmp[0];
                    i_list.timestamp = tmp[1];
                    i_list.chan = tmp[2];
                    i_list.nick = null;
                    i_list.msg = msg;
                }

                else if (tmp[0] == "[����]" || tmp[0] == "[�˸�]")
                {
                    Array.Copy(tmp, tmp2, tmp.Length); // tmp�� tmp2�� ����
                    Array.Clear(tmp2, 0, 1); // tmp2���� �޼����� �����ϰ� ����
                    string msg = string.Join(" ", tmp2, 2, tmp.Length - 2); // ���ڿ� �迭�� ���ڿ��� ����
                    i_list.type = tmp[0];
                    i_list.timestamp = tmp[1];
                    i_list.chan = null;
                    i_list.nick = null;
                    i_list.msg = msg;
                }

                else if (tmp[0] == "[�ʴ�]")
                {
                    i_list.type = tmp[0];
                    i_list.timestamp = tmp[1];
                    i_list.chan = tmp[2];
                    i_list.nick = tmp[3];
                    i_list.msg = null;
                    GCM.request(i_list.type, i_list.timestamp, i_list.chan, i_list.nick, i_list.msg, Properties.Settings.Default.gcm_id);
                }

                else if (tmp[0] == "[������]" || tmp[0] == "[������]")
                {
                    i_list.type = tmp[0];
                    i_list.timestamp = tmp[1];
                    i_list.chan = tmp[2];
                    i_list.nick = null;
                    if (tmp.Length > 3) i_list.msg = tmp[3];
                    else i_list.msg = null;
                }

                else if (tmp[0] == "[����ȭ����]")
                {
                    i_list.type = tmp[0];
                    i_list.timestamp = tmp[1];
                    i_list.chan = null;
                    i_list.nick = null;
                    i_list.msg = tmp[2];
                }
                else if (tmp[0] == "[�Ӹ���ȭ]" || tmp[0] == "[��ȭ����]" || tmp[0] == "[�Ӹ�ȣ��]"
                    || tmp[0] == "[����]" || tmp[0] == "[��Ƽ��]")
                {
                    Array.Copy(tmp, tmp2, tmp.Length); // tmp�� tmp2�� ����
                    Array.Clear(tmp2, 0, 3); // tmp2���� �޼����� �����ϰ� ����
                    string msg = string.Join(" ", tmp2, 3, tmp.Length - 3); // ���ڿ� �迭�� ���ڿ��� ����
                    i_list.type = tmp[0];
                    i_list.timestamp = tmp[1];
                    i_list.chan = null;
                    i_list.nick = tmp[2];
                    i_list.msg = msg;

                    if (tmp[0] == "[�Ӹ�ȣ��]" || tmp[0] == "[�Ӹ���ȭ]")
                    {
                        GCM.request(i_list.type, i_list.timestamp, i_list.chan, i_list.nick, i_list.msg, Properties.Settings.Default.gcm_id);
                    }
                }

                else if (tmp[0] == "[�и��]" || tmp[0] == "[�и��1]" || tmp[0] == "[�и��2]" || tmp[0] == "[�и��3]"
                    || tmp[0] == "[�и��4]" || tmp[0] == "[�и��5]")
                {
                    Array.Copy(tmp, tmp2, tmp.Length); // tmp�� tmp2�� ����
                    if (tmp.Length > 3)
                    {
                        Array.Clear(tmp2, 0, 4); // tmp2���� �޼����� �����ϰ� ����
                        string msg = string.Join(" ", tmp2, 4, tmp.Length - 4); // ���ڿ� �迭�� ���ڿ��� ����
                        i_list.type = tmp[0];
                        i_list.timestamp = tmp[1];
                        i_list.chan = tmp[2];
                        i_list.nick = tmp[3];
                        i_list.msg = msg;
                    }
                }

                else if (tmp[0] == "[ä�θ��]" || tmp[0] == "[��ȭ���]" || tmp[0] == "[���г���]")
                {
                    i_list.type = tmp[0];
                    i_list.timestamp = null;
                    i_list.chan = null;
                    i_list.nick = null;
                    i_list.msg = tmp[1];
                    if (tmp[1] == "n#ull") i_list.msg = null;
                }

                else if (item == "[��������]" || tmp[0] == "[�����߰�]" || tmp[0] == "[��������]")
                {
                    Array.Copy(tmp, tmp2, tmp.Length); // tmp�� tmp2�� ����
                    Array.Clear(tmp2, 0, 1); // tmp2���� �޼����� �����ϰ� ����
                    string msg = string.Join(" ", tmp2, 1, tmp.Length - 1); // ���ڿ� �迭�� ���ڿ��� ����
                    i_list.type = tmp[0];
                    i_list.timestamp = null;
                    i_list.chan = null;
                    i_list.nick = null;
                    i_list.msg = msg;
                }
                else if (item == "[shutdown]")
                {
                    Environment.Exit(0);
                }

                else if (tmp[0] == "[GCM]")
                {
                    Array.Copy(tmp, tmp2, tmp.Length); // tmp�� tmp2�� ����
                    Array.Clear(tmp2, 0, 5); // tmp2���� �޼����� �����ϰ� ����
                    string msg = string.Join(" ", tmp2, 5, tmp.Length - 5); // ���ڿ� �迭�� ���ڿ��� ����
                    i_list.type = tmp[1];
                    i_list.timestamp = tmp[2];
                    i_list.chan = tmp[3];
                    i_list.nick = tmp[4];
                    i_list.msg = msg;
                    GCM.request(i_list.type, i_list.timestamp, i_list.chan, i_list.nick, i_list.msg, Properties.Settings.Default.gcm_id);
                }
                else if (tmp[0] == "[DDE]")
                {
                    Properties.Settings.Default.gcm_msg = "dde_state";
                    Properties.Settings.Default.Save();
                }

                else
                {
                    Console.WriteLine("�� �� ���� ������ : " + AndroData);
                }

                try
                {
                    if (clientSocket.Connected && (i_list.type != null || whois.server != null))
                    {
                        send_data = JsonConvert.SerializeObject(i_list);
                        if (whois.server != null) send_data = JsonConvert.SerializeObject(whois);
                        Data = Encoding.UTF8.GetBytes(send_data);
                        byte[] sData = new byte[Data.Length + 2];
                        sData[0] = 0x02;
                        Data.CopyTo(sData, 1);
                        Array.Copy(Data, 0, sData, 1, Data.Length);
                        sData[Data.Length + 1] = 0x03;
                        // ����
                        clientSocket.Send(sData);
                        Console.WriteLine("[�۽�] " + send_data); // MONITOR
                        i_list.type = i_list.timestamp = i_list.chan = i_list.nick = i_list.msg = null;
                        whois.address = whois.name = whois.server = whois.chanlist = whois.state = whois.idle = whois.time = whois.idle = null;
                    }
                    else
                    {
                        // ����
                        send_data = JsonConvert.SerializeObject(i_list);
                        if (i_list.type != "[������]" && i_list.type != null)
                        {
                            File.AppendAllText("DDE_Logger.txt", send_data + "\r\n", Encoding.Default);
                            Console.WriteLine("[���] " + send_data);
                        }
                    }
                }
                catch (Exception)
                {
                    // ����
                    send_data = JsonConvert.SerializeObject(i_list);
                    if (i_list.type != "[������]" && i_list.type != null)
                    { 
                        File.AppendAllText("DDE_Logger.txt", send_data + "\r\n", Encoding.Default);
                        Console.WriteLine("[���] " + send_data);
                    }
                    
                }
                return PokeResult.Processed;
            }

        }

    }

}